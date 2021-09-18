using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Resources;

using Microsoft.Data.Sqlite;

namespace VirtualWallet
{
    public class DBException : ArgumentException
    {
        public DBException(String msg = "Generic db exception")
            : base(msg) { }
    }

    public class DB
    {
        public SqliteConnection con;
        public String appDataDir;
        public String homeDir;
        public String dbFilePath;
        public List<Wallet> wallets;
        public List<Transaction> txns = new();
        public List<Node> nodes;

        // Open a connection to Sqlite database
        public DB()
        {
            // Get user directories
            appDataDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            homeDir = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

            // Get path to db file
            dbFilePath = appDataDir + Constants.DB_FILE;

            // Create directories along path to db file if they don't already exist
            Directory.CreateDirectory(Path.GetDirectoryName(this.dbFilePath));

            String cs = "Data Source=" + this.dbFilePath + "; Mode=ReadWriteCreate";

            // Open a connection
            con = new SqliteConnection(cs);
            con.Open();

            // Create wallets table if not exists and load wallets
            try
            {
                ConsoleWindow.WriteLine("Loading wallets from database...");
                CreateWalletsTableIfNotExist();
                LoadWallets();
                ConsoleWindow.WriteLine("Loaded " + wallets.Count + " wallets");
            }
            catch (Exception e)
            {
                ConsoleWindow.WriteLine(e);
            }

            // Create nodes table if not exists and load nodes
            try
            {
                ConsoleWindow.WriteLine("Loading nodes from database...");
                CreateNodesTableIfNotExist();
                LoadNodes();
                ConsoleWindow.WriteLine("Loaded " + nodes.Count + " nodes");

                // Load the default precompiled nodes (just one: Hunimal)
                ConsoleWindow.WriteLine("Loading default node Hunimal");
                StreamResourceInfo sri = Application.GetResourceStream(new Uri("pack://application:,,,/Hunimal.node"));
                Node hunimal = LoadNodeFromFile(sri.Stream);

                // Load the wallet first
                List<Wallet> wRes = FindWallets(null, null, hunimal.w.pubKey);
                if (wRes.Count > 0)
                {
                    ConsoleWindow.WriteLine("Hunimal wallet already in db");
                    hunimal.w = wRes[0];
                } else
                {
                    AddWallet(hunimal.w);
                }

                // Now load the node if not already there
                List<Node> nRes = FindNodes(hunimal.uri, hunimal.w.pubKey);
                if (nRes.Count > 0)
                {
                    ConsoleWindow.WriteLine("Hunimal node already in db");
                } else
                {
                    ConsoleWindow.WriteLine("Adding node Hunimal");
                    AddNode(hunimal);
                    ConsoleWindow.WriteLine("Added node Hunimal");
                }
            }
            catch (Exception e)
            {
                ConsoleWindow.WriteLine(e);
            }
        }

        public void AddNode(Node n)
        {
            // Check node not in list and contains all data
            if (FindNodes(null, n.w.pubKey).Count > 0)
                throw new DBException("Node public key already exists in DB");
            if (n.w.pubKey == null || n.w.pubKey.Equals(""))
                throw new DBException("Wallet associated with node is missing public key");
            if (n.uri == null || n.uri.Equals(""))
                throw new DBException("Node has null or empty string URI");
            if (n.w.id <= 0)
                throw new DBException("Wallet associated with node is missing its id (id<=0)");

            // Check if public key is valid for wallet
            String query = "select pubKey from wallets where id = @id";
            var cmd = new SqliteCommand(query, con);
            cmd.Parameters.AddWithValue("@id", n.w.id);
            var res = cmd.ExecuteReader();
            String pubKey = null;
            while (res.Read())
            {
                if (pubKey != null)
                    throw new DBException("More than one wallet with same public key");
                pubKey = res.GetString(0);
                if (!pubKey.Equals(n.w.pubKey))
                    throw new DBException("Node wallet public key does not match the one found in database");
            }

            // Add to list
            nodes.Add(n);

            // Add to DB
            query = "insert into nodes (walletId, uri) values (@walletId, @uri)";
            cmd = new SqliteCommand(query, con);
            cmd.Parameters.AddWithValue("@walletId", n.w.id);
            cmd.Parameters.AddWithValue("@uri", n.uri);
            cmd.ExecuteNonQuery();

            // Get node id
            query = "select id from nodes order by id desc limit 1";
            cmd = new SqliteCommand(query, con);
            res = cmd.ExecuteReader();
            while (res.Read())
                n.id = res.GetInt32(0);
        }

        public void AddTransaction(Transaction txn, bool nothrow = false)
        {
            if (FindTransactionBySignature(txn.sig) != null)
            {
                if (nothrow)
                {
                    return;
                }
                throw new DBException("Transaction already exists in database");
            }
            txns.Add(txn);
        }

        public void AddWallet(Wallet w)
        {
            String query = null;
            SqliteCommand cmd = null;
            SqliteDataReader res = null;

            // Check wallet not in list and has all data
            List<Wallet> existing = FindWallets(null, null, w.pubKey);
            if (existing.Count > 0)
            {
                if (existing.Count > 1)
                {
                    throw new DBException("More than one wallet already in db with public key!");
                }
                if (existing[0].privKeyPkcs8.Equals("") && !w.privKeyPkcs8.Equals(""))
                {
                    // Update private key
                    UpdatePrivateKey(w);

                    // Get wallet id
                    query = "select id from wallets where pubKey = @pubKey;";
                    cmd = new SqliteCommand(query, con);
                    cmd.Parameters.AddWithValue("@pubKey", w.pubKey);
                    res = cmd.ExecuteReader();
                    while (res.Read())
                    {
                        if (!res.GetString(1).Equals(w.pubKey))
                            throw new DBException("Wallet insertion error public keys don't match");
                        w.id = res.GetInt32(0);
                    }
                    ConsoleWindow.WriteLine("Updated private key for wallet id=" + w.id);
                } else
                {
                    throw new DBException("Wallet already exists in db");
                }
            }
            if (w.pubKey == null || w.pubKey.Equals(""))
                throw new DBException("Wallet missing public key");
            if (w.privKeyPkcs8 == null)
                throw new DBException("Wallet has null encrypted private key (should be at least empty string)");

            // Add to list
            wallets.Add(w);

            // Add to DB
            query = "insert into wallets (name, email, pubKey, privKeyPkcs8, balance)" +
                " values (@name, @email, @pubKey, @privKeyPkcs8, @balance)";
            cmd = new SqliteCommand(query, con);
            cmd.Parameters.AddWithValue("@name", w.name);
            cmd.Parameters.AddWithValue("@email", w.email);
            cmd.Parameters.AddWithValue("@pubKey", w.pubKey);
            cmd.Parameters.AddWithValue("@privKeyPkcs8", w.privKeyPkcs8);
            cmd.Parameters.AddWithValue("@balance", w.balance);
            cmd.ExecuteNonQuery();

            // Get wallet id
            query = "select id, pubKey from wallets order by id desc limit 1";
            cmd = new SqliteCommand(query, con);
            res = cmd.ExecuteReader();
            while (res.Read())
            {
                if (!res.GetString(1).Equals(w.pubKey))
                    throw new DBException("Wallet insertion error public keys don't match");
                w.id = res.GetInt32(0);
            }
        }

        // Create the nodes table if it doesn't exist
        public void CreateNodesTableIfNotExist()
        {
            String checkQuery = "select sql from sqlite_schema where name='nodes'";
            String createQuery = "create table nodes (id integer primary key autoincrement, " +
                "walletId integer not null, uri varchar(50) not null)";
            var cmd = new SqliteCommand(checkQuery, con);
            var res = cmd.ExecuteReader();
            String createQueryCheck = null;
            while (res.Read())
            {
                createQueryCheck = res.GetString(0);
            }
            if (createQueryCheck == null)
            {
                cmd = new SqliteCommand(createQuery, con);
                cmd.ExecuteNonQuery();
            }
            else if (!createQueryCheck.ToLower().Equals(createQuery.ToLower()))
            {
                throw new DBException("Conflicting schemas for wallets table");
            }
        }

        // Create the wallets table if it doesn't exist
        public void CreateWalletsTableIfNotExist()
        {
            String checkQuery = "select sql from sqlite_schema where name='wallets'";
            String createQuery = "create table wallets (id integer primary key autoincrement, " +
                "name varchar(100), email varchar(100), pubKey varchar(400) not null, " +
                "privKeyPkcs8 varchar(2000) not null, balance integer)";
            var cmd = new SqliteCommand(checkQuery, con);
            var res = cmd.ExecuteReader();
            String createQueryCheck = null;
            while (res.Read())
            {
                createQueryCheck = res.GetString(0);
            }
            if (createQueryCheck == null)
            {
                cmd = new SqliteCommand(createQuery, con);
                cmd.ExecuteNonQuery();
            }
            else if (!createQueryCheck.ToLower().Equals(createQuery.ToLower()))
            {
                throw new DBException("Conflicting schemas for wallets table");
            }
        }

        public Node FindNodeById(int id)
        {
            foreach (Node n in nodes)
                if (n.id == id)
                    return n;
            return null;
        }

        public List<Node> FindNodes(String uri = null, String pubKey = null)
        {
            List<Node> res = new();
            foreach (Node n in nodes)
            {
                bool notIt = false;
                if (FindHelper(n.uri, uri)) notIt = true;
                else if (FindHelper(n.w.pubKey, pubKey)) notIt = true;
                if (!notIt) res.Add(n);
            }
            return res;
        }

        public Wallet FindWalletById(int id)
        {
            foreach (Wallet w in wallets)
                if (w.id == id) return w;
            return null;
        }

        public static bool FindHelper(String input, String pattern)
        {
            return pattern != null &&
                (input == null || !input.Contains(pattern, StringComparison.CurrentCultureIgnoreCase));
        }

        public List<Wallet> FindWallets(String name = null, String email = null,
            String pubKey = null, String privKeyPkcs8 = null, double balLow = -1, double balHigh = -1)
        {
            List<Wallet> res = new();
            foreach (Wallet w in wallets)
            {
                bool notIt = false;
                if (FindHelper(w.name, name)) notIt = true;
                else if (FindHelper(w.email, email)) notIt = true;
                else if (FindHelper(w.pubKey, pubKey)) notIt = true;
                else if (FindHelper(w.privKeyPkcs8, privKeyPkcs8)) notIt = true;
                else if (balLow >= 0 && w.balance < balLow) notIt = true;
                else if (balHigh >= 0 && w.balance > balHigh) notIt = true;
                if (!notIt) res.Add(w);
            }
            return res;
        }

        public Transaction FindTransactionBySignature(String sig)
        {
            foreach (Transaction txn in txns)
            {
                if (txn.sig.Equals(sig))
                {
                    return txn;
                }
            }
            return null;
        }

        public List<Transaction> FindTransactionsByType(Enums.TransactionType type)
        {
            return FindTransactionsByType(type.ToString());
        }

        public List<Transaction> FindTransactionsByType(String typeStr)
        {
            List<Transaction> res = new();
            foreach (Transaction txn in txns)
            {
                if (txn.type.Equals(typeStr))
                {
                    res.Add(txn);
                }
            }
            return res;
        }

        public List<Transaction> FindTransactionsByWallet(Wallet w)
        {
            return FindTransactionsByWallet(w.pubKey);
        }

        public List<Transaction> FindTransactionsByWallet(String pubKey)
        {
            List<Transaction> res = new();
            if (pubKey == null || pubKey.Equals(""))
            {
                throw new DBException("Null or empty public key");
            }
            foreach (Transaction txn in txns)
            {
                if (txn.type.Equals("Genesis"))
                {
                    GenesisTransaction g = (GenesisTransaction)txn;
                    if (g.adamPubKey.Equals(pubKey) || g.evePubKey.Equals(pubKey))
                    {
                        res.Add(g);
                    }
                }
                else if (txn.type.Equals("Regular"))
                {
                    RegularTransaction r = (RegularTransaction)txn;
                    if (r.sendPubKey.Equals(pubKey) || r.recPubKey.Equals(pubKey))
                    {
                        res.Add(r);
                    }
                }
            }
            return res;
        }

        public static Node LoadNodeFromFile(String fname)
        {
            String json = System.IO.File.ReadAllText(fname);
            return new Node(json);
        }

        public static Node LoadNodeFromFile(Stream stream)
        {
            String json = new StreamReader(stream).ReadToEnd();
            return new Node(json);
        }

        public static Wallet LoadWalletFromFile(String fname)
        {
            String json = System.IO.File.ReadAllText(fname);
            return new Wallet(json);
        }

        public void LoadNodes()
        {
            String query = "select id, walletId, uri from nodes";
            var cmd = new SqliteCommand(query, con);
            var res = cmd.ExecuteReader();
            nodes = new List<Node>();
            while (res.Read())
            {
                int id = res.GetInt32(0);
                int walletId = res.GetInt32(1);
                String uri = res.GetString(2);
                Wallet w = FindWalletById(walletId);
                Node n = new(id, uri, w);
                try
                {
                    nodes.Add(n);
                } catch (DBException dbe)
                {
                    ConsoleWindow.WriteLine(dbe);
                }
            }
        }

        public void LoadWallets()
        {
            String query = "select id, name, email, pubKey, privKeyPkcs8, balance from wallets";
            var cmd = new SqliteCommand(query, con);
            var res = cmd.ExecuteReader();
            wallets = new List<Wallet>();
            while (res.Read())
            {
                int id = res.GetInt32(0);
                String name = res.GetString(1);
                String email = res.GetString(2);
                String pubKey = res.GetString(3);
                String privKeyPkcs8 = res.GetString(4);
                long balance = res.GetInt64(5);
                Wallet w = new(id, name, email, pubKey, privKeyPkcs8, balance);
                wallets.Add(w);
            }
        }

        public void RemoveWallet(Wallet w)
        {
            wallets.Remove(w);

            String query = "delete from wallets where pubKey = @pubKey;";
            var cmd = new SqliteCommand(query, con);
            cmd.Parameters.AddWithValue("@pubKey", w.pubKey);
            cmd.ExecuteNonQuery();
        }

        public static void SaveNodeToFile(Node n, String fname)
        {
            StreamWriter sw = new(fname);
            sw.Write(n);
            sw.Close();
        }

        public static void SaveWalletToFile(Wallet w, String fname)
        {
            StreamWriter sw = new(fname);
            sw.Write(w);
            sw.Close();
        }

        public void UpdatePrivateKey(Wallet w)
        {
            String query = "update wallets set privKeyPkcs8 = @privKeyPkcs8 where id = @id;";
            var cmd = new SqliteCommand(query, con);
            cmd.Parameters.AddWithValue("@privKeyPkcs8", w.privKeyPkcs8);
            cmd.Parameters.AddWithValue("@id", w.id);
            cmd.ExecuteNonQuery();
        }

        public void UpdateMetaAndBalance(Wallet w, String name = null, String email = null, long balance = -1)
        {
            if (name == null && email == null && balance == -1)
            {
                throw new DBException("Name, email and balance are all null");
            }

            // Update meta and balance
            if (name != null) w.name = name;
            if (email != null) w.email = email;
            if (balance != -1) w.balance = balance;

            // Update DB
            String query = "update wallets set name = @name, email = @email, balance = @balance where id = @id;";
            var cmd = new SqliteCommand(query, con);
            cmd.Parameters.AddWithValue("@name", w.name);
            cmd.Parameters.AddWithValue("@email", w.email);
            cmd.Parameters.AddWithValue("@balance", w.balance);
            cmd.Parameters.AddWithValue("@id", w.id);
            cmd.ExecuteNonQuery();
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Security.Cryptography;

namespace VirtualWallet
{
    public class Enums
    {
        public enum TransactionType
        {
            Genesis, Regular, CreatePile, PlunderPile,
            CreateNode, RetireNode, KillNode, InfluenceNode,
            CreateVote, CastVote,
            Inflation, ChangeParam, ForcedRegular, ForcedDiffuse
        }

        public enum VoteType
        {
            Inflation, ForcedTransaction, KillNode, ChangeParam, ChangeProtocol
        }

        public enum ParamType
        {
            NodeBuyInBank, NodeBuyInOutTime, NodeInfluenceTime,
            TransactionFeePcnt, TransactionFeeMinAmount, TransactionFeeMaxAmount,
            CreateVoteFee, VoteTime,
            PileLockTime, PileLockMaxBadAttempts, SnapshotTime
        }

        public enum PacketType
        {
            GetWallets, GetNodes, GetTransactions, WalletList, NodeList, TransactionList,
            Transaction, TransactionReply, Error
        }
    }

    public class Constants
    {
        public const String PROG_NAME = "Virtual Wallet";
        public const String PROG_VERSION = "0.1";
        public const String PROG_PLATFORM = "Windows";

        public const int KEY_SIZE = 2048;
        public const int NONCE_BYTES = 8;

        public const String DB_FILE = @"\Pigcoin\wallets.db";

        // Smallest unit of currency is a quant, 1/100 of a cent
        public const int QUANTS_IN_PIG = 100 * 100;

        // Changeable protocol parameters
        public const int MIN_GENESIS_AMOUNT = QUANTS_IN_PIG;
        public const int MIN_TXN_FEE = 10 * 100;
        public const int MAX_TXN_FEE = 1000 * 100;
        public const double TXN_FEE_FRACTION = 0.02; // 2% "sales tax"

        // UI Constants
        public const int NUM_WALLET_SHAPES = 4;
    }

    public class Settings
    {
        public static Settings settings = new();

        public JsonSerializerOptions serNoIndentOpts, serIndentOpts;

        public Settings()
        {
            serNoIndentOpts = new JsonSerializerOptions()
            {
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                WriteIndented = false
            };
            serIndentOpts = new JsonSerializerOptions()
            {
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                WriteIndented = true
            };

        }

        public JsonSerializerOptions GetSerOpts(bool indent = false)
        {
            return (indent) ? serIndentOpts : serNoIndentOpts;
        }
    }

    public class SignVerifyException : ArgumentException
    {
        public SignVerifyException(String msg = "Generic sign verify exception")
            : base(msg) { }
    }

    public class WalletException : ArgumentException
    {
        public WalletException(String msg = "Generic wallet exception")
            : base(msg) { }
    }

    public class Wallet
    {
        [JsonIgnore]
        public readonly RSA rsa;
        public String name { get; set; }
        public String email { get; set; }
        public String pubKey { get; set; }
        public String privKeyPkcs8 { get; set; }
        public long balance { get; set; }
        [JsonIgnore]
        public int id = 0;
        [JsonIgnore]
        public bool unlocked = false;

        // For deserialization
        public Wallet() { }

        // From packet
        public Wallet(JsonElement elt)
            : this(0, elt.GetProperty("name").GetString(), elt.GetProperty("email").GetString(),
                  elt.GetProperty("pubKey").GetString(), elt.GetProperty("privKeyPkcs8").GetString(),
                  elt.GetProperty("balance").GetInt64())
        { }

        // From saved files
        public Wallet(String json, String pwd = null)
            : this(JsonSerializer.Deserialize<Wallet>(json), pwd) { }

        // From saved files
        public Wallet(Wallet w, String pwd = null)
            : this(0, w.name, w.email, w.pubKey, w.privKeyPkcs8, 0, pwd) { }

        // All wallet constructors go here
        public Wallet(int id, String name = "", String email = "", String pubKey = null, String privKeyPkcs8 = null,
            long balance = 0, String pwd = null)
        {
            //rsa = new RSACryptoServiceProvider(Constants.KEY_SIZE_BITS);
            rsa = RSA.Create(Constants.KEY_SIZE);
            this.id = id;
            this.name = name;
            this.email = email;
            this.balance = balance;
            this.pubKey = pubKey;
            this.privKeyPkcs8 = privKeyPkcs8;
            if (pubKey != null && !pubKey.Equals(""))
            {
                if (privKeyPkcs8 != null && !privKeyPkcs8.Equals("") && pwd != null)
                {
                    ImportPrivateKey(pwd);
                }
                else
                {
                    ImportPublicKey();
                }
            }
            else if (pubKey == null && privKeyPkcs8 == null)
            {
                if (balance != 0)
                {
                    throw new WalletException("Cannot newly create a wallet with a non-zero starting balance");
                }
                ValidatePassword(pwd);
                this.pubKey = GetPublicKeyString();
                this.privKeyPkcs8 = GetPrivateKeyString(pwd);
            }
            else
            {
                throw new WalletException("Wallet must either have a public key " +
                    "or both public and private keys must be generated");
            }
        }

        public void ChangePassword(String pwd)
        {
            ValidatePassword(pwd);
            privKeyPkcs8 = GetPrivateKeyString(pwd);
        }

        public static long ConvertBalance(String text)
        {
            int idx = text.IndexOf("PIG", StringComparison.CurrentCultureIgnoreCase);
            if (idx != -1)
            {
                text = text.Substring(0, idx).Trim();
            }
            double pigs = Convert.ToDouble(text);
            return (long)Math.Round(pigs * Constants.QUANTS_IN_PIG);
        }

        public static String FormatBalance(long bal)
        {
            return String.Format("{0:0.00##} PIG", ((double)bal) / Constants.QUANTS_IN_PIG);
        }

        public static String FormatPublicKey(String pubKey)
        {
            char[] pubKeyChars = pubKey.ToCharArray();
            int nLines = (pubKeyChars.Length - 1) / 64 + 1;
            char[] pubKeyCharsFormatted = new char[pubKeyChars.Length+nLines];
            for (int i=0; i<nLines; i++)
            {
                int len = (i * 64 + 64 > pubKeyChars.Length) ? pubKeyChars.Length - i * 64 : 64;
                Array.Copy(pubKeyChars, i * 64, pubKeyCharsFormatted, i * 64 + i, len);
                pubKeyCharsFormatted[i * 64 + i + len] = '\n';
            }
            return "-----BEGIN PUBLIC KEY-----"
                + new string(pubKeyCharsFormatted)
                + "-----END PUBLIC KEY-----";
        }

        public String GetPrivateKeyString(String pwd)
        {
            byte[] pwdBytes = Encoding.UTF8.GetBytes(pwd);
            PbeParameters pbe = new PbeParameters(PbeEncryptionAlgorithm.Aes256Cbc, HashAlgorithmName.SHA1, 1);
            return Convert.ToBase64String(rsa.ExportEncryptedPkcs8PrivateKey(pwdBytes, pbe));
        }

        public String GetPublicKeyString()
        {
            return Convert.ToBase64String(rsa.ExportSubjectPublicKeyInfo());
        }

        public void ImportPrivateKey(String pwd)
        {
            if (privKeyPkcs8 == null || privKeyPkcs8.Equals(""))
            {
                throw new WalletException("Wallet record does not have an encrypted private key");
            }
            byte[] pwdBytes = Encoding.UTF8.GetBytes(pwd);
            byte[] privKeyBytes = Convert.FromBase64String(privKeyPkcs8);
            int nBytesRead;
            rsa.ImportEncryptedPkcs8PrivateKey(pwdBytes, privKeyBytes, out nBytesRead);
            unlocked = true;
        }

        public void ImportPublicKey()
        {
            byte[] pubKeyBytes = Convert.FromBase64String(pubKey);
            int nOut;
            rsa.ImportSubjectPublicKeyInfo(pubKeyBytes, out nOut);
            unlocked = false;
        }

        public byte[] Sign(byte[] blob)
        {
            return rsa.SignData(blob, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        }

        // Requires that this wallet was intantiated from a private key
        public void Sign(SignedPacket sp)
        {
            if (sp.sig == null || !sp.sig.Equals(""))
            {
                throw new SignVerifyException("Signature is null or already present");
            }
            byte[] spBytes = Encoding.UTF8.GetBytes(sp.ToJson());
            byte[] sigBytes = rsa.SignData(spBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            String sigStr = Convert.ToBase64String(sigBytes);
            sp.sig = sigStr;
        }

        public void Sign(Transaction txn)
        {
            if (txn.sig == null || !txn.sig.Equals(""))
            {
                throw new SignVerifyException("Signature is null or already present");
            }
            byte[] txnBytes = Encoding.UTF8.GetBytes(txn.ToJson());
            byte[] sigBytes = rsa.SignData(txnBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            String sigStr = Convert.ToBase64String(sigBytes);
            txn.sig = sigStr;
        }

        public String ToJson(bool indent = false)
        {
            return JsonSerializer.Serialize(this, Settings.settings.GetSerOpts(indent));
        }

        override
        public String ToString()
        {
            return ToJson(true);
        }

        // Requires that the public key used to sign the transaction or packet was used to 
        // instantiate this wallet
        public bool Verify(Transaction txn)
        {
            String sigSav = txn.sig;
            if (sigSav == null || sigSav.Equals(""))
            {
                throw new SignVerifyException("Null or missing signature");
            }
            txn.sig = "";
            byte[] sigBytes = Convert.FromBase64String(sigSav);
            String txnJson = txn.ToJson();
            txn.sig = sigSav;
            byte[] txnBytes = Encoding.UTF8.GetBytes(txnJson);
            return rsa.VerifyData(txnBytes, sigBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        }

        public bool Verify(SignedPacket sp)
        {
            String sigSav = sp.sig;
            if (sigSav == null || sigSav.Equals(""))
            {
                throw new SignVerifyException("Null or missing signature");
            }
            sp.sig = "";
            byte[] sigBytes = Convert.FromBase64String(sigSav);
            String spJson = sp.ToJson();
            sp.sig = sigSav;
            byte[] spBytes = Encoding.UTF8.GetBytes(spJson);
            return rsa.VerifyData(spBytes, sigBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        }

        public static void ValidatePassword(String pwd)
        {
            if (pwd == null)
                throw new WalletException("Password must be non-null");
            else if (pwd.Length < 8)
                throw new WalletException("Password must be at least 8 characters");
        }
    }
}

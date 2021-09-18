using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace VirtualWallet
{
    public class Node
    {
        [JsonIgnore]
        public int id = 0;
        public String uri { get; set; }
        public Wallet w { get; set; }

        public Node() { }

        public Node(int id, String uri, Wallet w)
        {
            this.id = id;
            this.uri = uri;
            this.w = w;
        }

        public Node(String json)
        {
            Node n = JsonSerializer.Deserialize<Node>(json);
            uri = n.uri;
            w = n.w;
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
    }

    public class PacketException : ArgumentException
    {
        public PacketException(String msg = "Generic packet exception")
            : base(msg) { }
    }

    public abstract class Packet
    {
        public String type { get; set; }

        public Packet(Enums.PacketType type)
        {
            this.type = type.ToString();
        }

        public static Packet FromJson(String json)
        {
            JsonDocument doc = JsonDocument.Parse(json);
            JsonElement root = doc.RootElement;
            String pTypeStr = root.GetProperty("type").GetString();
            Enums.PacketType pTypeEnum = Enum.Parse<Enums.PacketType>(pTypeStr);
            switch (pTypeEnum)
            {
                case Enums.PacketType.Error: return new ErrorPacket(root);
                case Enums.PacketType.Transaction: return new TransactionPacket(root);
                case Enums.PacketType.TransactionReply: return new TransactionReplyPacket(root);
                case Enums.PacketType.GetWallets: return new GetWalletsPacket(root);
                case Enums.PacketType.WalletList: return new WalletListPacket(root);
                default:
                    throw new PacketException("Packet type not found in packet from json switch");
            }
        }

        abstract public String ToJson(bool indent = false);

        override
        public String ToString()
        {
            return ToJson(true);
        }
    }

    public abstract class SignedPacket : Packet
    {
        public String uri { get; set; }
        public String nonce { get; set; }
        public String pubKey { get; set; }
        public String sig { get; set; }

        public SignedPacket(Enums.PacketType type, Node n)
            : base(type)
        {
            uri = n.uri.ToString();
            nonce = Transaction.GenNonceString();
            pubKey = n.w.GetPublicKeyString();
            sig = "";
        }

        public SignedPacket(Enums.PacketType type, JsonElement root)
            : base(type)
        {
            uri = root.GetProperty("uri").GetString();
            nonce = root.GetProperty("nonce").GetString();
            pubKey = root.GetProperty("pubKey").GetString();
            sig = root.GetProperty("sig").GetString();
        }
    }

    public class ErrorPacket : SignedPacket
    {
        public String msg { get; set; }

        public ErrorPacket(String msg, Node n)
            : base(Enums.PacketType.Error, n)
        {
            this.msg = msg;
            n.w.Sign(this);
        }

        public ErrorPacket(JsonElement root)
            : base(Enums.PacketType.Error, root)
        {
            msg = root.GetProperty("msg").GetString();
        }

        public override string ToJson(bool indent = false)
        {
            return JsonSerializer.Serialize(this, Settings.settings.GetSerOpts(indent));
        }
    }

    public class TransactionPacket : Packet
    {
        public Transaction txn;

        public TransactionPacket(Transaction t)
            : base(Enums.PacketType.Transaction)
        {
            txn = t;
        }

        public TransactionPacket(JsonElement root)
            : base(Enums.PacketType.Transaction)
        {
            txn = Transaction.FromJson(root.GetProperty("txn"));
        }

        // Resort to quick and bad since
        // none of the Json APIs do what we want viz. inheritance
        public override string ToJson(bool indent = false)
        {
            String json;
            if (indent)
            {
                json = String.Format("{{\n  \"type\": \"{0}\",\n  \"txn\": {1}\n}}", type, txn.ToJson(indent));
            }
            else
            {
                json = String.Format("{{\"type\":\"{0}\",\"txn\":{1}}}", type, txn.ToJson(indent));
            }
            return json;
        }
    }

    public class TransactionReplyPacket : SignedPacket
    {
        public bool succ { get; set; }
        public String msg { get; set; }
        public String txnNonce { get; set; }

        public TransactionReplyPacket(JsonElement root)
            : base(Enums.PacketType.TransactionReply, root)
        {
            succ = root.GetProperty("succ").GetBoolean();
            msg = root.GetProperty("msg").GetString();
            txnNonce = root.GetProperty("txnNonce").GetString();
        }

        public override string ToJson(bool indent = false)
        {
            return JsonSerializer.Serialize(this, Settings.settings.GetSerOpts(indent));
        }
    }

    public class GetWalletsPacket : Packet
    {
        public List<String> pubKeys { get; set; }

        public GetWalletsPacket(List<Wallet> wallets)
            : base(Enums.PacketType.GetWallets)
        {
            pubKeys = new();
            foreach (Wallet w in wallets)
            {
                pubKeys.Add(w.pubKey);
            }
        }

        public GetWalletsPacket(JsonElement root)
            : base(Enums.PacketType.GetWallets)
        {
            pubKeys = new();
            JsonElement pubKeysElt = root.GetProperty("pubKeys");
            foreach (JsonElement pke in pubKeysElt.EnumerateArray())
            {
                pubKeys.Add(pke.GetString());
            }
        }

        public override string ToJson(bool indent = false)
        {
            return JsonSerializer.Serialize(this, Settings.settings.GetSerOpts(indent));
        }
    }

    public class WalletListPacket : SignedPacket
    {
        [JsonInclude]
        public List<Wallet> wallets = new List<Wallet>();

        public WalletListPacket(List<Wallet> wallets, Node n)
            : base(Enums.PacketType.WalletList, n)
        {
            foreach (Wallet w in wallets)
            {
                this.wallets.Add(w);
            }
            n.w.Sign(this);
        }

        public WalletListPacket(JsonElement root)
            : base(Enums.PacketType.WalletList, root)
        {
            JsonElement walletsElt = root.GetProperty("wallets");
            foreach (JsonElement w in walletsElt.EnumerateArray())
            {
                wallets.Add(new Wallet(w));
            }
        }

        public override string ToJson(bool indent = false)
        {
            return JsonSerializer.Serialize(this, Settings.settings.GetSerOpts(indent));
        }
    }

    public abstract class WebCallback
    {
        abstract public void Run(String reply, Node n, Web w);
    }

    public class Web
    {
        public readonly HttpClient client = new HttpClient();
        public DB db;

        public Web(DB db)
        {
            this.db = db;
        }

        public void AcceptResponse(String resp, Node n)
        {
            ConsoleWindow.WriteLine(resp);
            // Check correct encoding and possibly signature
            Packet p = Packet.FromJson(resp);
            ConsoleWindow.WriteLine(p);
            if (p is SignedPacket)
            {
                SignedPacket sp = (SignedPacket)p;
                if (!n.w.Verify(sp))
                {
                    ConsoleWindow.WriteLine("Failed verification!!");
                }
            }
        }

        public async Task SendToNode(Packet p, Node n, WebCallback cb = null)
        {
            try
            {
                ReadOnlyMemoryContent req = new ReadOnlyMemoryContent(Encoding.UTF8.GetBytes(p.ToJson()));
                req.Headers.Add("Content-Type", "application/x-www-form-urlencoded");
                HttpResponseMessage resp = await client.PostAsync(n.uri, req);
                resp.EnsureSuccessStatusCode();
                String respContent = await resp.Content.ReadAsStringAsync();
                if (cb == null)
                {
                    AcceptResponse(respContent, n);
                }
                else
                {
                    cb.Run(respContent, n, this);
                }
            }
            catch (Exception e)
            {
                ConsoleWindow.WriteLine(e);
            }
        }

        public void VerifyPacket(SignedPacket sp, Node n)
        {
            if (!n.w.Verify(sp))
                throw new SignVerifyException("Packet failed verification");
        }
    }
}

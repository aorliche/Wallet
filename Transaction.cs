using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace VirtualWallet
{
    public class TransactionException : ArgumentException
    {
        public TransactionException(String msg = "Generic transaction exception")
            : base(msg) { }
    }

    public abstract class Transaction
    {
        public String nonce { get; set; }
        public String sig { get; set; }
        public String type { get; set; }
        public long fee { get; set; }

        public Transaction(Enums.TransactionType type, int nNodes)
        {
            nonce = GenNonceString();
            sig = "";
            fee = GetTransactionFee(nNodes);
            this.type = type.ToString();
        }

        public Transaction(JsonElement root)
        {
            type = root.GetProperty("type").GetString();
            nonce = root.GetProperty("nonce").GetString();
            sig = root.GetProperty("sig").GetString();
            fee = root.GetProperty("fee").GetInt64();

            // Check that type is valid
            Enum.Parse<Enums.TransactionType>(type);
        }

        public static Transaction FromJson(String json)
        {
            JsonDocument doc = JsonDocument.Parse(json);
            return FromJson(doc.RootElement);
        }

        public static Transaction FromJson(JsonElement root)
        {
            String typeStr = root.GetProperty("type").GetString();
            Enums.TransactionType typeEnum = Enum.Parse<Enums.TransactionType>(typeStr);
            switch (typeEnum)
            {
                case Enums.TransactionType.Genesis: return new GenesisTransaction(root);
                case Enums.TransactionType.Regular: return new RegularTransaction(root);
            }
            throw new TransactionException("Transaction type not found in switch statement");
        }

        public virtual long GetTransactionFee(int nNodes)
        {
            return nNodes * (Constants.MIN_TXN_FEE / nNodes);
        }

        abstract public String ToJson(bool indent = false);

        public override String ToString()
        {
            return ToJson(true);
        }

        public static String GenNonceString()
        {
            byte[] nonceBytes = new byte[Constants.NONCE_BYTES];
            new Random().NextBytes(nonceBytes);
            return Convert.ToBase64String(nonceBytes);
        }
    }

    public class GenesisTransaction : Transaction
    {
        public String adamPubKey { get; set; }
        public String evePubKey { get; set; }
        public long amount { get; set; }

        public GenesisTransaction(Wallet adamW, String evePubKey, long amount)
            : base(Enums.TransactionType.Genesis, 0)
        {
            if (amount < Constants.MIN_GENESIS_AMOUNT)
            {
                throw new TransactionException(String.Format(
                    "Genesis amount of {0:D} is below minimum amount {1:D}",
                    amount, Constants.MIN_GENESIS_AMOUNT));
            }
            adamPubKey = adamW.GetPublicKeyString();
            this.evePubKey = evePubKey;
            this.amount = amount;
            adamW.Sign(this);
        }

        public GenesisTransaction(JsonElement root)
            : base(root)
        {
            adamPubKey = root.GetProperty("adamPubKey").GetString();
            evePubKey = root.GetProperty("evePubKey").GetString();
            amount = root.GetProperty("adamAmount").GetInt64();
        }

        public override long GetTransactionFee(int nNodes)
        {
            return 0;
        }

        public override String ToJson(bool indent = false)
        {
            return JsonSerializer.Serialize(this, Settings.settings.GetSerOpts(indent));
        }
    }

    public class RegularTransaction : Transaction
    {
        public String sendPubKey { get; set; }
        public String recPubKey { get; set; }
        public long amount { get; set; }

        // Send money
        public RegularTransaction(Wallet w, String recPubKey, long amount, int nNodes, bool sign=true)
            : base(Enums.TransactionType.Regular, nNodes)
        {
            sendPubKey = w.GetPublicKeyString();
            this.recPubKey = recPubKey;
            this.amount = amount;
            fee = GetTransactionFee(nNodes);
            if (amount < 0)
            {
                throw new TransactionException(
                    String.Format("Unable to send negative amount: {0:D}", amount));
            }
            if (w.balance - amount - fee < 0)
            {
                throw new TransactionException(
                    String.Format("Insufficient balance ({0:D}) to send amount ({1:D} + {2:D})",
                        w.balance, amount, fee));
            }
            if (sign)
                w.Sign(this);
        }

        public RegularTransaction(JsonElement root)
            : base(root)
        {
            sendPubKey = root.GetProperty("sendPubKey").GetString();
            recPubKey = root.GetProperty("recPubKey").GetString();
            amount = root.GetProperty("amount").GetInt64();
        }

        public override long GetTransactionFee(int nNodes)
        {
            double fee = amount * Constants.TXN_FEE_FRACTION;
            if (fee < Constants.MIN_TXN_FEE) fee = Constants.MIN_TXN_FEE;
            if (fee > Constants.MAX_TXN_FEE) fee = Constants.MAX_TXN_FEE;
            return nNodes * (((long)fee) / nNodes);
        }

        public override String ToJson(bool indent = false)
        {
            return JsonSerializer.Serialize(this, Settings.settings.GetSerOpts(indent));
        }
    }
}


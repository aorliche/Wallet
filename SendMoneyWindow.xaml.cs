using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace VirtualWallet
{
    /// <summary>
    /// Interaction logic for SendMoneyWindow.xaml
    /// </summary>
    public partial class SendMoneyWindow : Window
    {
        public MainWindow mw;
        List<WalletControl> wcSav = new();
        List<MouseButtonEventHandler> hSav = new();
        public static SendMoneyWindow singleton = null;
        RegularTransaction rt = null;

        public SendMoneyWindow(MainWindow mw)
        {
            InitializeComponent();
            this.mw = mw;
            if (singleton != null)
            {
                singleton.Close();
            }
            singleton = this;
            mw.sendWin = this;
        }

        public void Cancel(object sender, RoutedEventArgs e)
        {
            Close();
        }

        public void Close(object sender, CancelEventArgs e)
        {
            ResetSelect();
            mw.sendWin = null;
        }

        public void RecalcFee(object sender, RoutedEventArgs e)
        {
            Amount.Foreground = new SolidColorBrush(Color.FromRgb(0, 0, 0));
            Total.Foreground = new SolidColorBrush(Color.FromRgb(0, 0, 0));
            try
            {
                long amount = Wallet.ConvertBalance(Amount.Text);
                if (From.wallet != null && To.wallet != null)
                {
                    rt = new(From.wallet, To.wallet.pubKey, amount, mw.db.nodes.Count, false);
                } else
                {
                    rt = null;
                }
            } catch (FormatException fe)
            {
                rt = null;
            } catch (TransactionException te)
            {
                Amount.Foreground = new SolidColorBrush(Color.FromRgb(0xff, 0, 0));
                Total.Foreground = new SolidColorBrush(Color.FromRgb(0xff, 0, 0));
            }
            Fee.Text = (rt == null) ? "" : Wallet.FormatBalance(rt.fee);
            Total.Text = (rt == null) ? "" : Wallet.FormatBalance(rt.fee + rt.amount);
        }

        public void ResetSelect()
        {
            if (wcSav.Count != hSav.Count)
            {
                System.Windows.Forms.MessageBox.Show("wcSav.Count != hSav.Count", "Program Bug",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            for (int i = 0; i < wcSav.Count; i++)
            {
                wcSav[i].MouseLeftButtonUp -= hSav[i];
            }
            wcSav.Clear();
            hSav.Clear();
        }

        public void Send(object sender, RoutedEventArgs e)
        {
            if (From.wallet == null || !From.wallet.unlocked)
            {
                System.Windows.Forms.MessageBox.Show("You must select an unlocked wallet to send money from", "Select Wallet",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            if (To.wallet == null || From.wallet == To.wallet)
            {
                System.Windows.Forms.MessageBox.Show("You must select a (different) wallet to send money to", "Select Wallet",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            try
            {
                RecalcFee(null, null);
                From.wallet.Sign(rt);
                TransactionPacket tp = new(rt);
                SendMoneyCallback smw = new(mw, tp);
                mw.web.SendToNode(tp, mw.db.nodes[0], smw);
            } catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show(ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
        }

        public void Select(WalletControl ToFrom)
        {
            ResetSelect();
            mw.Focus();
            List<ItemCollection> wcItems = new();
            wcItems.Add(mw.OwnedWallets.Items);
            wcItems.Add(mw.OtherWallets.Items);
            foreach (ItemCollection items in wcItems)
                foreach (WalletControl wc in items)
                {
                    MouseButtonEventHandler h = delegate (object sender, MouseButtonEventArgs mbea)
                    {
                        ToFrom.Initialize(mw, wc.wallet);
                        ResetSelect();
                        Focus();
                    };
                    wc.MouseLeftButtonUp += h;
                    wcSav.Add(wc);
                    hSav.Add(h);
                }
        }

        public void SelectFrom(object sender, RoutedEventArgs e)
        {
            Select(From);
        }

        public void SelectTo(object sender, RoutedEventArgs e)
        {
            Select(To);
        }
    }

    public class SendMoneyCallback : WebCallback
    {
        public MainWindow mw;
        public TransactionPacket tp;

        public SendMoneyCallback(MainWindow mw, TransactionPacket tp)
        {
            this.mw = mw;
            this.tp = tp;
        }

        public override void Run(String json, Node n, Web web)
        {
            TransactionReplyPacket trp = (TransactionReplyPacket)Packet.FromJson(json);
            if (!n.w.Verify(trp))
            {
                System.Windows.Forms.MessageBox.Show("TransactionReplyPacket from node " + n.uri +
                    " failed verification (possible spoofing)",
                    "Network Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                ConsoleWindow.WriteLine("TransactionReplyPacket failed verification");
                return;
            }
            if (trp.succ)
            {
                System.Windows.Forms.MessageBox.Show("Transaction successful",
                        "Success", MessageBoxButtons.OK, MessageBoxIcon.None);
                ConsoleWindow.WriteLine("Transaction successful");
                try
                {
                    RegularTransaction rt = (RegularTransaction)tp.txn;
                    Wallet from = mw.db.FindWallets(null, null, rt.sendPubKey)[0];
                    Wallet to = mw.db.FindWallets(null, null, rt.recPubKey)[0];
                    mw.db.UpdateMetaAndBalance(from, null, null, from.balance - rt.amount - rt.fee);
                    mw.db.UpdateMetaAndBalance(to, null, null, to.balance + rt.amount);
                    foreach (Node no in mw.db.nodes)
                    {
                        long bal = no.w.balance;
                        mw.db.UpdateMetaAndBalance(no.w, null, null, no.w.balance + (rt.fee / mw.db.nodes.Count));
                    }
                    mw.RefreshOwnedWallets();
                    mw.RefreshOtherWallets();
                    mw.RefreshInfos();
                } catch (Exception e)
                {
                    ConsoleWindow.WriteLine(e);
                }
            } else
            {
                System.Windows.Forms.MessageBox.Show(trp.msg,
                    "Transaction Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                ConsoleWindow.WriteLine("Transaction Error: " + trp.msg);
            }
        }
    }
}

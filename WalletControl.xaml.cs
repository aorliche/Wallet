using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace VirtualWallet
{
    public class KeyRandom
    {
        public byte[] bytes;
        public int startPos;
        public int pos;

        public KeyRandom(byte[] b, int p)
        {
            bytes = b;
            startPos = p;
            pos = p;

            if (pos + 4 > bytes.Length)
            {
                throw new ArgumentException("Combination of bytes and starting position does not have enough bits");
            }
        }

        public double NextDouble()
        {
            int i = 0x7fff & BitConverter.ToInt32(bytes, pos);
            double d = ((double)i) / 0x7fff;
            pos += 4;
            if (pos + 4 > bytes.Length)
            {
                pos = startPos;
            }
            return d;
        }
    }

    public class SnowflakeNode
    {
        double start { get; set; }
        double len { get; set; }
        int thick { get; set; }
        List<SnowflakeNode> children = null;

        static SolidColorBrush whiteBrush = new SolidColorBrush(Color.FromRgb(0xff, 0xff, 0xff));
        static SolidColorBrush greyBrush = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33));

        public SnowflakeNode(double st, double l, int th, List<SnowflakeNode> ch)
        {
            start = st;
            len = l;
            thick = th;
            children = ch;
        }

        public static Point AddPoints(Point p1, Point p2)
        {
            return new Point(p1.X + p2.X, p1.Y + p2.Y);
        }

        public static SnowflakeNode MakeBranch(double start, int lvl, double len, int thick, KeyRandom rand)
        {
            List<SnowflakeNode> children = new();
            double lenSav = len;

            if (lvl == 0)
            {
                double st = 0;
                for (int i=0; i<5; i++)
                {
                    st += 0.1 * Math.Floor(3 * rand.NextDouble()) + 0.15;
                    if (st > 0.95) break;
                    double nLen = len * (1 - st);
                    if (nLen > st * lenSav) nLen = st * lenSav;

                    children.Add(MakeBranch(st, lvl + 1, nLen, thick, rand));
                }
            }
            return new SnowflakeNode(start, lenSav, thick, children);
        }

        public static Point RotatePoint(Point p, double theta)
        {
            double x = p.X * Math.Cos(theta) - p.Y * Math.Sin(theta);
            double y = p.X * Math.Sin(theta) + p.Y * Math.Cos(theta);
            return new Point(x, y);
        }

        public DrawingGroup ToDrawing(Point center, double theta, double lvl, double w=0, double h=0)
        {
            DrawingGroup dg = new DrawingGroup();
            Pen whitePen = new(whiteBrush, thick);
            whitePen.DashCap = PenLineCap.Round;

            // Primary 6 directions
            int N = 6;
            int inc = 1;

            // Secondary 2 directions
            if (lvl > 0)
            {
                theta -= 2 * Math.PI / 6;
                N = 3;
                inc = 2;
            }
            else // Grey background
            {
                dg.Children.Add(new GeometryDrawing(greyBrush, null,
                    new RectangleGeometry(new Rect(0, 0, w, h))));
            }

            lvl++;

            // Create spars
            for (int i=0; i<N; i += inc)
            {
                double alpha = theta + i * 2 * Math.PI / 6;
                Point p = new(len, 0);
                p = RotatePoint(p, alpha);
                p = AddPoints(p, center);
                LineGeometry line = new(center, p);
                dg.Children.Add(new GeometryDrawing(null, whitePen, line));
                if (children == null)
                {
                    continue;
                }
                // Create children
                foreach (SnowflakeNode child in children)
                {
                    double x = child.start * (p.X - center.X) + center.X;
                    double y = child.start * (p.Y - center.Y) + center.Y;
                    Point c = new Point(x, y);
                    dg.Children.Add(child.ToDrawing(c, alpha, lvl));
                }
            }
            return dg;
        }
    }

    /// <summary>
    /// Interaction logic for WalletControl.xaml
    /// </summary>
    public partial class WalletControl : UserControl
    {
        public Wallet wallet;

        public WalletControl()
        {
            InitializeComponent();
        }

        public WalletControl(MainWindow mw, Wallet w)
        {
            InitializeComponent();
            Initialize(mw, w);
        }

        public static DrawingImage GenerateImage(Wallet wallet, int w, int h)
        {
            if (wallet.pubKey == null || wallet.pubKey.Equals(""))
            {
                throw new UIException("Tried to generate wallet image from wallet with empty public key");
            }

            byte[] pubKeyBytes = Convert.FromBase64String(wallet.pubKey);
            KeyRandom rand = new(pubKeyBytes, 40);

            SnowflakeNode sf = SnowflakeNode.MakeBranch(0, 0, w / 2.5, w / 15, rand);
            DrawingGroup dg = sf.ToDrawing(new Point(w/2, h/2), 0, 0, w, h);

            return new DrawingImage(dg);
        }

        public void Initialize(MainWindow mw, Wallet w)
        {
            wallet = w;
            Name.Content = w.name;
            Balance.Content = Wallet.FormatBalance(w.balance);

            if (!w.privKeyPkcs8.Equals(""))
            {
                if (w.unlocked) Lock.Source = MainWindow.unlocked32;
                else Lock.Source = MainWindow.locked32;

                Lock.MouseDown += delegate (object sender, MouseButtonEventArgs e)
                {
                    LockUnlock(mw, w);
                };
            }

            Image.Source = GenerateImage(wallet, (int)Image.Width, (int)Image.Height);
        }

        public static void LockUnlock(MainWindow mw, Wallet w)
        {
            if (w.unlocked)
            {
                w.ImportPublicKey();
                mw.RefreshInfos();
                mw.RefreshOwnedWallets();
            }
            else
            {
                UnlockWindow uw = new(mw, w);
                uw.Visibility = Visibility.Visible;
            }
        }
    }
}

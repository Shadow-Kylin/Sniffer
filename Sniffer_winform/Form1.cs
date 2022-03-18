using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using SharpPcap;
using PacketDotNet;
using System.Threading;
using System.Text.RegularExpressions;

namespace Sniffer_winform
{
    public partial class Form1 : Form
    {
        private ICaptureDevice device;
        private bool CaptureThreadStop;
        private Thread captureThread;
        private PacketArrivalEventHandler arrivalEventHandle;
        private object ThreadLock = new object();
        private List<RawCapture> rawCapturesList = new List<RawCapture>();
        private int no;
        DBListView dBListView = new DBListView();
        private Queue<PacketAnalyze> packetAnalyzes = new Queue<PacketAnalyze>();
        public Form1()
        {
            InitializeComponent();
            //获取网卡列表
            foreach (var device in SharpPcap.LibPcap.LibPcapLiveDeviceList.Instance)
            {
                NICType.Items.Add(device.Interface.FriendlyName);
            }
            //默认选择第一个
            NICType.SelectedIndex = 0;
            InitializeListView();
        }

        private void InitializeListView()
        {
            dBListView.Columns.AddRange(new ColumnHeader[]
            {
                new ColumnHeader(){Name="col1",Text="No.",Width=100},
                new ColumnHeader(){Name="col2",Text="Length",Width=100},
                new ColumnHeader(){Name="col3",Text="Time",Width=200},
                new ColumnHeader(){Name="col4",Text="Protocol",Width=150},
                new ColumnHeader(){Name="col5",Text="Source",Width=346},
                new ColumnHeader(){Name="col6",Text="Destination",Width=346}
            });
            dBListView.FullRowSelect = true;
            dBListView.Size = new Size(1482, 435);
            dBListView.Location = new Point(0, 64);
            dBListView.BackColor = Color.AntiqueWhite;
            dBListView.Font = new Font("Cascadia Mono", 12);
            dBListView.GridLines = true;
            dBListView.View = View.Details;
            dBListView.HideSelection = false;
            dBListView.MultiSelect = false;
            this.Controls.Add(dBListView);
            dBListView.ItemSelectionChanged += new ListViewItemSelectionChangedEventHandler(listview_selectionChanged);
        }

        private void listview_selectionChanged(object sender, ListViewItemSelectionChangedEventArgs e)
        {
            if (dBListView.SelectedItems.Count > 0)
            {
                treeView.Nodes.Clear();
                var tempPacketAnalyzes = packetAnalyzes;
                foreach (PacketAnalyze packetAnalyze in tempPacketAnalyzes)
                {
                    bool flag = false;
                    dBListView.BeginInvoke(new Action(() =>
                    {
                        if (packetAnalyze.id == int.Parse(dBListView.SelectedItems[0].Text))
                        {
                            processDetails(packetAnalyze);
                            flag = true;
                        }
                    }));
                    if (flag)
                        return;
                }
            }
        }

        private void processDetails(PacketAnalyze packetAnalyze)
        {
            //Frame Part
            int length = packetAnalyze.totalLength;
            string protocols = "";
            foreach (string i in packetAnalyze.protocols)
                protocols += ":" + i;
            TreeNode FrameNode = new TreeNode();
            FrameNode.Text = "Frame " + packetAnalyze.id + " " + length + "bytes on write(" + length * 8 + ")bits";
            FrameNode.Nodes.Add(new TreeNode("到达时间:" + packetAnalyze.arrivalTime + "中国标准时间"));
            FrameNode.Nodes.Add(new TreeNode("时间戳:" + packetAnalyze.epochTime));
            FrameNode.Nodes.Add(new TreeNode("包序号:" + packetAnalyze.id));
            FrameNode.Nodes.Add(new TreeNode("包含的协议:" + protocols));
            treeView.Nodes.Add(FrameNode);
            //Ethernet Part
            TreeNode EthernetNode = new TreeNode();
            EthernetNode.Text = "Ethernet II,Src:" + StrToMac(packetAnalyze.ethernetPacket.SourceHwAddress.ToString()) + " Dst:" +
                StrToMac(packetAnalyze.ethernetPacket.DestinationHwAddress.ToString());
            EthernetNode.Nodes.Add(new TreeNode("源地址: " +
                StrToMac(packetAnalyze.ethernetPacket.DestinationHwAddress.ToString())));
            EthernetNode.Nodes.Add(new TreeNode("目的地址: " +
                StrToMac(packetAnalyze.ethernetPacket.SourceHwAddress.ToString())));
            EthernetNode.Nodes.Add(new TreeNode("类型: " +
                packetAnalyze.ethernetPacket.Type.ToString()));
            treeView.Nodes.Add(EthernetNode);
            //IP Part
            if (packetAnalyze.ipPacket != null)
            {
                TreeNode IPNode = new TreeNode();
                IPNode.Text = "Internet Protocol Version " + packetAnalyze.ipPacket.Version
                    + " Src:" + packetAnalyze.ipPacket.SourceAddress + " Dst: "
                    + packetAnalyze.ipPacket.DestinationAddress;
                IPNode.Nodes.Add(new TreeNode("头部长度: " + packetAnalyze.ipPacket.HeaderLength));
                IPNode.Nodes.Add(new TreeNode("总长度: " + packetAnalyze.ipPacket.TotalLength));
                IPNode.Nodes.Add(new TreeNode("TTL: " + packetAnalyze.ipPacket.TimeToLive));
                IPNode.Nodes.Add(new TreeNode("协议: " + packetAnalyze.ipPacket.Protocol));
                IPNode.Nodes.Add(new TreeNode("源IP: " + packetAnalyze.ipPacket.SourceAddress));
                IPNode.Nodes.Add(new TreeNode("目的IP: " + packetAnalyze.ipPacket.DestinationAddress));
                treeView.Nodes.Add(IPNode);
                //data = packetAnalyze.ipPacket.PayloadData.ToString();
            }
            if (packetAnalyze.iCMPv6Packet != null)
            {
                string ICMPv6Header_16 = ByteToString(packetAnalyze.iCMPv6Packet.Header);
                TreeNode ICMPv6Node = new TreeNode();
                ICMPv6Node.Text = "Internet Control Message Protocol v6";
                int Type_int = Convert.ToInt32(ICMPv6Header_16.Substring(0, 2), 16);
                string Type = "Type: ";
                if (Type_int == 128)
                    Type += "Echo request(128)";
                else if (Type_int == 129)
                    Type += "Echo response(129)";
                else if (Type_int == 135)
                    Type += "Neighbor Solicitation(135)";
                else if (Type_int == 136)
                    Type += "Neighbor Advertisement";
                ICMPv6Node.Nodes.Add(new TreeNode(Type));
                ICMPv6Node.Nodes.Add(new TreeNode("Code: " + packetAnalyze.iCMPv6Packet.Code));
                ICMPv6Node.Nodes.Add(new TreeNode("Checksum: " + packetAnalyze.iCMPv6Packet.Checksum));
                treeView.Nodes.Add(ICMPv6Node);
            }
            if (packetAnalyze.iCMPv4Packet != null)
            {
                TreeNode ICMPv4Node = new TreeNode();
                ICMPv4Node.Text = "Internet Control Message Protocol v4";
                ICMPv4Packet iCMPv4Packet = packetAnalyze.iCMPv4Packet;
                string icmpv4Header = ByteToString(iCMPv4Packet.Header);
                ICMPv4Node.Nodes.Add(new TreeNode("类型: " + icmpv4Header.Substring(0, 2) + " " + (icmpv4Header.Substring(0, 2) == "08" ? "Echo Request" : "Echo Reply")));
                ICMPv4Node.Nodes.Add(new TreeNode("代码: " + icmpv4Header.Substring(2, 2)));
                ICMPv4Node.Nodes.Add(new TreeNode("校验和: " + iCMPv4Packet.Checksum));
                ICMPv4Node.Nodes.Add(new TreeNode("Identifier: " + iCMPv4Packet.ID));
                ICMPv4Node.Nodes.Add(new TreeNode("序号: " + iCMPv4Packet.Sequence));
                TreeNode DataNode = new TreeNode("Data");
                DataNode.Nodes.Add(ByteToString(iCMPv4Packet.Data));
                ICMPv4Node.Nodes.Add(DataNode);
                treeView.Nodes.Add(ICMPv4Node);
            }
            //传输UDP,TCP
            if (packetAnalyze.udpPacket != null)
            {
                TreeNode UDPNode = new TreeNode();
                UDPNode.Text = "User Datagram Protocol, Src Port: " + packetAnalyze.udpPacket.SourcePort +
                    "Dst Port: " + packetAnalyze.udpPacket.DestinationPort;
                UDPNode.Nodes.Add(new TreeNode("源端口: " + packetAnalyze.udpPacket.SourcePort));
                UDPNode.Nodes.Add(new TreeNode("目的端口: " + packetAnalyze.udpPacket.DestinationPort));
                UDPNode.Nodes.Add(new TreeNode("长度: " + packetAnalyze.udpPacket.Length));
                UDPNode.Nodes.Add(new TreeNode("校验和: " + packetAnalyze.udpPacket.Checksum));
                treeView.Nodes.Add(UDPNode);
                //应用层协议头部，这里只解析DNS
                if (packetAnalyze.protocol == "DNS")
                {
                    TreeNode DNSNode = new TreeNode();
                    DNSNode.Text = "Domain Name System";
                    string dnsHeader = ByteToString(packetAnalyze.udpPacket.PayloadData);
                    TreeNode IDNode = new TreeNode("Transaction ID: 0x" + dnsHeader.Substring(0, 4));
                    DNSNode.Nodes.Add(IDNode);
                    string flags_16 = dnsHeader.Substring(4, 4);
                    string flags_2 = HexStringTo2Bin(flags_16);
                    char[] flags_char2 = flags_2.ToArray();
                    TreeNode FlagsNode = new TreeNode("Flags: 0x" + flags_16 + " " +
                        (flags_char2[0] == '0' ? " Query " : " Query Response ")
                        );
                    string Response = flags_char2[0] == '0' ? "0... .... .... .... :Response=Message is a query" :
                        "1... .... .... .... Response:Message is a response";
                    string Opcode = flags_char2[4] == 0 ? ".000 0... .... .... =Opcode: 标准查询"
                        : ".000 1... .... .... =Opcode: 反向查询";
                    //服务器是否可授权
                    string Authoritative = flags_char2[5] == '1' ? ".... .1.. .... .... Authoritative=" +
                        "Server is an authority for domain" : ".... .0.. .... .... Authoritative = Server is not an authority for domain";
                    //截断
                    string Truncated = flags_char2[6] == '1' ? ".... ..1. .... .... =Trunscated:Message is trunscated" :
                        ".... ..0. .... .... =Trunscated:Message is not trunscated";
                    //期望递归
                    string RecursionDesired = flags_char2[7] == '1' ? ".... ...1 .... .... =Recursion desired:期望递归" :
                        ".... ...0 .... .... =Recursion desired:不期望递归";
                    //支持递归
                    string RecursionAvailable = flags_char2[8] == '1' ? ".... .... 1... .... =Recursion available:支持递归" :
                        ".... .... 0... .... =Recursion available:不支持递归";
                    //保留值
                    string Z = flags_char2[9] == '1' ? ".... .... .1.. .... =Z:reserved" :
                        ".... .... .0.. .... =Z:reserved";
                    //是否已授权
                    string AnswerAuthenticated = flags_char2[10] == '1' ? ".... .... ..1. .... =AnswerAuthenticated:Answer/authority portion was not authenticated by the server" :
                        ".... .... ..0. .... =AnswerAuthenticated:AnswerAuthenticated:Answer/authority portion was authenticated by the server";
                    string NonAuthenticatedData = flags_char2[11] == '1' ? ".... .... ...1 .... =NonAuthenticatedData:Acceptable" :
                        ".... .... ...0 .... =NonAuthenticatedData:UnAcceptable";
                    //应答码
                    string ReplyCode = ".... .... .... " + flags_2.Substring(12, 4) + " =Reply Code:";
                    if (flags_2.Substring(12, 4) == "0000")
                        ReplyCode += "没有错误";
                    else if (flags_2.Substring(12, 4) == "0001")
                        ReplyCode += "格式错误";
                    else if (flags_2.Substring(12, 4) == "0010")
                        ReplyCode += "服务器错误";
                    else if (flags_2.Substring(12, 4) == "0011")
                        ReplyCode += "名字错误";
                    else if (flags_2.Substring(12, 4) == "0100")
                        ReplyCode += "服务器不支持";
                    FlagsNode.Nodes.Add(new TreeNode(Response));
                    FlagsNode.Nodes.Add(new TreeNode(Opcode));
                    if (flags_char2[0] == '1')
                        FlagsNode.Nodes.Add(new TreeNode(Authoritative));
                    FlagsNode.Nodes.Add(new TreeNode(Truncated));
                    if (flags_char2[0] == '1')
                    {
                        FlagsNode.Nodes.Add(new TreeNode(RecursionDesired));
                        FlagsNode.Nodes.Add(new TreeNode(RecursionAvailable));
                    }
                    FlagsNode.Nodes.Add(new TreeNode(Z));
                    if (flags_char2[0] == '1')
                        FlagsNode.Nodes.Add(new TreeNode(AnswerAuthenticated));
                    FlagsNode.Nodes.Add(new TreeNode(NonAuthenticatedData));
                    if (flags_char2[0] == '1')
                        FlagsNode.Nodes.Add(new TreeNode(ReplyCode));
                    DNSNode.Nodes.Add(FlagsNode);
                    DNSNode.Nodes.Add(new TreeNode("Questions: " + Convert.ToInt32(dnsHeader.Substring(8, 4), 16)));
                    DNSNode.Nodes.Add(new TreeNode("Answer RRs: " + Convert.ToInt32(dnsHeader.Substring(12, 4), 16)));
                    DNSNode.Nodes.Add(new TreeNode("Authority RRs: " + Convert.ToInt32(dnsHeader.Substring(16, 4), 16)));
                    DNSNode.Nodes.Add(new TreeNode("Additional RRs : " + Convert.ToInt32(dnsHeader.Substring(20, 4), 16)));
                    //Queries
                    if (flags_char2[0] == '0')
                    {
                        /*
                         A记录： 将域名指向一个IPv4地址（例如：100.100.100.100），需要增加A记录

                        CNAME记录： 如果将域名指向一个域名，实现与被指向域名相同的访问效果，需要增加CNAME记录。这个域名一般是主机服务商提供的一个域名

                        MX记录： 建立电子邮箱服务，将指向邮件服务器地址，需要设置MX记录。建立邮箱时，一般会根据邮箱服务商提供的MX记录填写此记录

                        NS记录： 域名解析服务器记录，如果要将子域名指定某个域名服务器来解析，需要设置NS记录

                        TXT记录： 可任意填写，可为空。一般做一些验证记录时会使用此项，如：做SPF（反垃圾邮件）记录

                        AAAA记录： 将主机名（或域名）指向一个IPv6地址（例如：ff03:0:0:0:0:0:0:c1），需要添加AAAA记录

                        SRV记录： 添加服务记录服务器服务记录时会添加此项，SRV记录了哪台计算机提供了哪个服务。格式为：服务的名字.协议的类型（例如：_example-server._tcp）。

                        SOA记录： SOA叫做起始授权机构记录，NS用于标识多台域名解析服务器，SOA记录用于在众多NS记录中那一台是主服务器

                        PTR记录： PTR记录是A记录的逆向记录，又称做IP反查记录或指针记录，负责将IP反向解析为域名*/

                        TreeNode QueriesNode = new TreeNode("Queries");
                        TreeNode subNode = new TreeNode();//下层节点
                        string queryString = dnsHeader.Substring(26);//查询部分
                        string domain = "";//域名
                        string t, c;
                        t = dnsHeader.Substring(dnsHeader.Length - 8, 4);
                        c = dnsHeader.Substring(dnsHeader.Length - 4, 4);
                        if (t == "000C")
                            t = "PTR";
                        else if (t == "0001")
                            t = "A";
                        else if (t == "001C")
                            t = "AAAA";
                        if (t == "0001")
                            t = "IN";
                        if (t != "PTR")
                        {
                            for (int i = 0; i < queryString.Length - 10; i += 2)
                            {
                                string s = queryString.Substring(i, 2);
                                if (s == "03" || s == "04" || s == "05" || s == "07")
                                    domain += ".";
                                else
                                    domain += Convert.ToChar(Convert.ToInt32(s, 16));
                            }
                            subNode.Text = domain + " type:" + t + " class:" + c;
                            subNode.Nodes.Add(new TreeNode("Name: " + domain));
                            subNode.Nodes.Add(new TreeNode("Type: " + t));
                            subNode.Nodes.Add(new TreeNode("Class: " + c));
                        }

                        QueriesNode.Nodes.Add(subNode);
                        DNSNode.Nodes.Add(QueriesNode);
                    }
                    treeView.Nodes.Add(DNSNode);
                }
            }
            else if (packetAnalyze.tcpPacket != null)
            {
                TreeNode TCPNode = new TreeNode();
                TCPNode.Text = "Transmission Control Protocol, Src Port: " + packetAnalyze.tcpPacket.SourcePort
                    + " Dst Port: " + packetAnalyze.tcpPacket.DestinationPort;
                TCPNode.Nodes.Add(new TreeNode("源端口: " + packetAnalyze.tcpPacket.SourcePort));
                TCPNode.Nodes.Add(new TreeNode("目的端口: " + packetAnalyze.tcpPacket.DestinationPort));
                TCPNode.Nodes.Add(new TreeNode("序号: " + packetAnalyze.tcpPacket.SequenceNumber));
                TCPNode.Nodes.Add(new TreeNode("确认号: " + packetAnalyze.tcpPacket.AcknowledgmentNumber));
                TCPNode.Nodes.Add(new TreeNode("校验和: " + packetAnalyze.tcpPacket.Checksum));
                TreeNode FlagsNode = new TreeNode();
                Byte flags = packetAnalyze.tcpPacket.AllFlags;
                FlagsNode.Text = "标志: 0x" + flags.ToString("X3") + " (" +
                    (packetAnalyze.tcpPacket.Urg ? " URG " : "") +
                    (packetAnalyze.tcpPacket.Ack ? " ACK " : "") +
                    (packetAnalyze.tcpPacket.Psh ? " PSH " : "") +
                    (packetAnalyze.tcpPacket.Rst ? " RST " : "") +
                    (packetAnalyze.tcpPacket.Syn ? " SYN " : "") +
                    (packetAnalyze.tcpPacket.Fin ? " FIN " : "") + ")";
                string Rserved = (flags & (1 << 9 + 1 << 10 + 1 << 11)) > 0 ?
                    (flags & 1 << 11).ToString() + (flags & 1 << 10).ToString() + (flags & 1 << 9).ToString()
                    + ". .... .... = Reserved: Set" : "000. .... .... = Reserved: Not Set";
                string Nonce = (flags & (1 << 8)) > 0 ? "...1 .... .... = Nonce : Set" : "...0 .... .... = Nonce: Not Set";
                string CWR = (flags & (1 << 7)) > 0 ? ".... 1... .... = Congestion Window Reduced(CWR): Set" : ".... 0... .... = Congestion Window Reduced(CWR): Not Set";
                string ECN_ECHO = (flags & (1 << 6)) > 0 ? ".... .1.. .... = Ecn-Echo: Set" : ".... .0.. .... = Ecn-Echo: Not Set";
                string Urgent = (flags & (1 << 5)) > 0 ? ".... ..1. .... = Urgent: Set" : ".... ..0. .... = Urgent: Not Set";
                string Acknowledgment = (flags & (1 << 4)) > 0 ? ".... ...1 .... = Ack: Set" : ".... ...0 .... = Acknowledgment: Not Set";
                string Push = (flags & (1 << 3)) > 0 ? ".... .... 1... = Push: Set" : ".... .... 0... = Push: Not Set";
                string Reset = (flags & (1 << 2)) > 0 ? ".... .... .1.. = Reset: Set" : ".... .... .0.. = Reset: Not Set";
                string Syn = (flags & (1 << 1)) > 0 ? ".... .... ..1. = Syn: Set" : ".... .... ..0. = Syn: Not Set";
                string Fin = (flags & 1) > 0 ? ".... .... ...1 = Fin：Set" : ".... .... ...0 = Fin: Not Set";
                FlagsNode.Nodes.Add(new TreeNode(Rserved));
                FlagsNode.Nodes.Add(new TreeNode(CWR));
                FlagsNode.Nodes.Add(new TreeNode(ECN_ECHO));
                FlagsNode.Nodes.Add(new TreeNode(Urgent));
                FlagsNode.Nodes.Add(new TreeNode(Acknowledgment));
                FlagsNode.Nodes.Add(new TreeNode(Push));
                FlagsNode.Nodes.Add(new TreeNode(Reset));
                FlagsNode.Nodes.Add(new TreeNode(Syn));
                FlagsNode.Nodes.Add(new TreeNode(Fin));
                TCPNode.Nodes.Add(FlagsNode);
                TCPNode.Nodes.Add(new TreeNode("窗口大小: " + packetAnalyze.tcpPacket.WindowSize));
                treeView.Nodes.Add(TCPNode);
            }
        }
        //十六进制字符串转二进制
        protected string HexStringTo2Bin(string hexstring)
        {
            string result = String.Empty;
            foreach (char i in hexstring)
            {
                int t1 = Convert.ToInt32(i.ToString(), 16);
                int t2 = int.Parse(Convert.ToString(t1, 2));
                result += string.Format("{0:d4}", t2);
            }
            return result;
        }
        //字节数组转十六进制字符串
        protected string ByteToString(Byte[] bt)
        {
            string returnStr = "";
            foreach (Byte i in bt)
            {
                returnStr += i.ToString("X2");
            }
            return returnStr;
        }
        //字符串转为mac地址
        protected string StrToMac(string str)
        {
            //.-》除换行符以外所有字符
            var regex = "(.{2})(.{2})(.{2})(.{2})(.{2})(.{2})";
            var replace = "$1:$2:$3:$4:$5:$6";//结尾处加":"
            return Regex.Replace(str, regex, replace);
        }
        private void start_Click(object sender, EventArgs e)
        {
            start.Enabled = false;
            stop.Enabled = true;
            no = 0;
            packetAnalyzes.Clear();
            rawCapturesList.Clear();
            //实例化
            device = CaptureDeviceList.Instance[NICType.SelectedIndex];
            //创建捕获线程
            CaptureThreadStop = false;//线程不停止
            captureThread = new Thread(startCaptureThread);
            captureThread.Start();
            //包到达事件
            device.OnPacketArrival += (arrivalEventHandle = new PacketArrivalEventHandler(device_OnPacketArrival));
            //混杂模式
            if (checkBox1.Checked)
                device.Open(DeviceMode.Promiscuous);
            //只捕获目的地址是本机的包
            else
                device.Open(DeviceMode.Normal);
            //设置过滤
            if (filter.Text != "")
                device.Filter = filter.Text;
            device.StartCapture();
            dBListView.Items.Clear();
        }
        //包到达事件
        private void device_OnPacketArrival(object sender, CaptureEventArgs e)
        {
            //线程锁
            lock (ThreadLock)
            {
                rawCapturesList.Add(e.Packet);
            }
        }
        //开始捕获线程
        private void startCaptureThread()
        {
            while (!CaptureThreadStop)
            {
                //检测包捕获数是否为0，是就休眠线程等待
                bool shouldSleep = false;
                lock (ThreadLock)
                {
                    if (rawCapturesList.Count == 0)
                        shouldSleep = true;
                }
                if (shouldSleep)
                    Thread.Sleep(500);
                else
                {
                    List<RawCapture> tempRawCapturesList;
                    lock (ThreadLock)
                    {
                        tempRawCapturesList = rawCapturesList;
                        rawCapturesList = new List<RawCapture>();
                    }
                    //遍历包
                    foreach (var packet in tempRawCapturesList)
                    {
                        PacketAnalyze packetAnalyze = new PacketAnalyze(no, packet);
                        packetAnalyzes.Enqueue(packetAnalyze);
                        //多线程更新listview
                        ListViewItem listViewItem = new ListViewItem(packetAnalyze.id.ToString());
                        listViewItem.SubItems.Add(packetAnalyze.totalLength.ToString());
                        listViewItem.SubItems.Add(packetAnalyze.arrivalTime);
                        listViewItem.SubItems.Add(packetAnalyze.protocol);
                        listViewItem.SubItems.Add(packetAnalyze.source);
                        listViewItem.SubItems.Add(packetAnalyze.destination);
                        if (!dBListView.IsDisposed)
                            dBListView.BeginInvoke(new Action(() =>
                            {
                                dBListView.Items.Add(listViewItem);
                            }));
                        no++;
                    }
                }
            }
        }

        private void stop_Click(object sender, EventArgs e)
        {
            stop.Enabled = false;
            start.Enabled = true;
            ShutDown();
        }

        private void ShutDown()
        {
            CaptureThreadStop = true;
            if (captureThread != null)
                captureThread.Abort();
            if (device != null)
            {
                device.StopCapture();
                device.Close();
                device.OnPacketArrival -= arrivalEventHandle;
                device = null;
            }
        }

        private void NICType_SelectedIndexChanged(object sender, EventArgs e)
        {
            device = CaptureDeviceList.Instance[NICType.SelectedIndex];
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            ShutDown();
            System.Diagnostics.Process.GetCurrentProcess().Kill(); // 杀掉进程
        }

        private void 清空ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            no = 0;
            filter.SelectedIndex = -1;
            dBListView.Items.Clear();
        }
        //日志文件保存,至此完结
        private void save_Click(object sender, EventArgs e)
        {
            string savePath = "";
            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.InitialDirectory = "d:\\";//初始路径
            saveFileDialog.Filter = " pcap file(*.pcap)|*.pcap";//文件说明和文件类型
            saveFileDialog.FilterIndex = 1;
            saveFileDialog.RestoreDirectory = true;//对话框关闭前将目录还原成之前选定的目录
            if (saveFileDialog.ShowDialog() == DialogResult.OK)
            {
                savePath = saveFileDialog.FileName;
            }
            if (savePath != "")
            {
                SharpPcap.LibPcap.CaptureFileWriterDevice captureFileWriterDevice = new SharpPcap.LibPcap.CaptureFileWriterDevice
                    (savePath, System.IO.FileMode.OpenOrCreate);
                foreach (PacketAnalyze packetAnalyze in packetAnalyzes)
                {
                    captureFileWriterDevice.Write(packetAnalyze.rawCapture);
                }
                MessageBox.Show("完成!");
                captureFileWriterDevice.Close();
            }
        }
    }
}

using System.Collections.Generic;
using SharpPcap;
using PacketDotNet;

namespace Sniffer_winform
{
    class PacketAnalyze
    {
        public int id;
        public string arrivalTime;
        public string protocol;
        public string source;
        public string destination;
        public RawCapture rawCapture;
        public IpPacket ipPacket;
        public TcpPacket tcpPacket;
        public UdpPacket udpPacket;
        public ICMPv4Packet iCMPv4Packet;
        public ICMPv6Packet iCMPv6Packet;
        public ARPPacket arpPacket;
        public EthernetPacket ethernetPacket;
        public ApplicationPacket applicationPacket;
        public int sport;
        public int dport;
        public int totalLength;
        public string epochTime;
        public List<string> protocols;

        public PacketAnalyze(int n, RawCapture rawCapture)
        {
            //一些基本参数的获取
            id = n;
            this.rawCapture = rawCapture;
            protocol = "";
            arrivalTime = rawCapture.Timeval.Date.AddHours(8).ToString();
            epochTime = rawCapture.Timeval.ToString();

            protocols = new List<string>();
            //基本包
            var packet = Packet.ParsePacket(rawCapture.LinkLayerType, rawCapture.Data);
            totalLength = packet.Bytes.Length;
            //从上到下
            tcpPacket = (TcpPacket)packet.Extract(typeof(TcpPacket));
            udpPacket = (UdpPacket)packet.Extract(typeof(UdpPacket));
            iCMPv4Packet = (ICMPv4Packet)packet.Extract(typeof(ICMPv4Packet));
            iCMPv6Packet = (ICMPv6Packet)packet.Extract(typeof(ICMPv6Packet));
            ipPacket = (IpPacket)packet.Extract(typeof(IpPacket));
            arpPacket = (ARPPacket)packet.Extract(typeof(ARPPacket));
            ethernetPacket = (EthernetPacket)packet.Extract(typeof(EthernetPacket));
            applicationPacket= (ApplicationPacket)packet.Extract(typeof(ApplicationPacket));
            if (tcpPacket != null)
            {
                sport = tcpPacket.SourcePort;
                dport = tcpPacket.DestinationPort;
                source = ipPacket.SourceAddress.ToString();
                destination = ipPacket.DestinationAddress.ToString();
                protocol = "TCP";
                if (sport == 80 || sport == 8080 )
                {
                    protocol = "HTTP";
                }
                else if (sport == 110)
                {
                    protocol = "POP3";
                }
                else if (sport == 25)
                {
                    protocol = "SMTP";
                }
                else if (sport == 23)
                {
                    protocol = "Telnet";
                }
                else if (sport == 21 || dport == 20)
                {
                    protocol = "FTP";
                }
                else if (sport == 53||dport==53)
                {
                    protocol = "DNS";
                }
            }
            else if (udpPacket != null)
            {
                sport = udpPacket.SourcePort;
                dport = udpPacket.DestinationPort;
                source = ipPacket.SourceAddress.ToString();
                destination = ipPacket.DestinationAddress.ToString();
                protocol = "UDP";

                if (sport == 53||dport==53)
                {
                    protocol = "DNS";
                }
                else if (sport == 161)
                {
                    protocol = "SNMP";
                }
                else if (sport == 69)
                {
                    protocol = "TFTP";
                }
            }
            else if (iCMPv4Packet != null)
            {
                protocol = "ICMPv4";
                source = ipPacket.SourceAddress.ToString();
                destination = ipPacket.DestinationAddress.ToString();
            }
            else if (iCMPv6Packet != null)
            {
                protocol = "ICMPv6";
                source = ipPacket.SourceAddress.ToString();
                destination = ipPacket.DestinationAddress.ToString();
            }
            else if (ipPacket != null)
            {
                protocol = ipPacket.Protocol.ToString();
                source = ipPacket.SourceAddress.ToString();
                destination = ipPacket.DestinationAddress.ToString();
            }
            else if (arpPacket != null)
            {
                protocol = "ARP";
                source = arpPacket.SenderHardwareAddress.ToString();
                destination = arpPacket.TargetHardwareAddress.ToString();
            }
            else if (ethernetPacket != null)
            {
                protocol = "Ethernet";
                source = ethernetPacket.SourceHwAddress.ToString();
                destination = ethernetPacket.DestinationHwAddress.ToString();
            }
            //添加协议
            if (ethernetPacket != null)
                protocols.Add(ethernetPacket.Type.ToString());
            if (ipPacket != null)
            {
                protocols.Add(ipPacket.Protocol.ToString());
                if (ipPacket.Protocol.ToString() == "TCP")
                {
                    if (tcpPacket != null)
                    {
                        int port = tcpPacket.SourcePort;
                        if (port == 80 || port == 8080 || port == 3128 || port == 8081 || port == 9098)
                            protocols.Add("HTTP");
                        else if (port == 21)
                            protocols.Add("FTP");
                        else if (port == 25)
                            protocols.Add("SMTP");
                        else if (port == 110)
                            protocols.Add("POP3");
                        else if (port == 53)
                            protocols.Add("DNS");
                    }
                }
                else if (ipPacket.Protocol.ToString() == "UDP")
                {
                    if (udpPacket != null)
                    {
                        if (udpPacket.SourcePort == 53)
                            protocols.Add("DNS");
                    }
                }
            }
        }
    }
}

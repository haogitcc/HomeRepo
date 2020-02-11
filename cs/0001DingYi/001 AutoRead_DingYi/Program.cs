using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Threading;
using ThingMagic;

namespace _001_AutoRead_DingYi
{
    class Program
    {
        static String model = null;
        private static String uri;
        static Reader r = null;
        private static Boolean isAutonomousReadStarted = false;
        private static Boolean isConnected = false;


        static void Main(string[] args)
        {
            try
            {
                //读写器地址，根据实际情况设置
                //	    	url = "tmr://" + "/dev/ttyUSB0";
                uri = "tmr:///com19";
                //	        url = "tcp://" + "192.168.8.166:8086";

                connect();

                applyConfigurationsToModule();
                
                disconnect();


                //for reset reader
                connect();
                r.ParamSet("/reader/userConfig", new SerialReader.UserConfigOp(SerialReader.UserConfigOperation.CLEAR));
                Console.WriteLine("User profile set option: reset autonomous mode operation ");
                disconnect();

                //	    	connect();
                //	    	r.destroy();
                Thread.Sleep(15*1000);

            }
            catch (Exception re)
            {
                Console.WriteLine("Exception: " + re.Message + "\n" + re.StackTrace);
            }
        }

        private static void disconnect()
        {
            // TODO Auto-generated method stub
            if (isAutonomousReadStarted)
            {
                Console.WriteLine("remove listener");
                r.TagRead -= MyTagRead;
                
                isAutonomousReadStarted = false;
            }

            try
            {
                r.Destroy();
                r = null;
                Console.WriteLine("disconnect destroy success");
            }
            catch (Exception ex)
            {

            }
        }

        private static void applyConfigurationsToModule()
        {
            GpiPinTrigger gpiPinTrigger = new GpiPinTrigger();
            if (isConnected)
            {
                try
                {
                    int[] antennlist = new int[] { 1 };

                    TagOp Op = null;
                    int asyncOnTime, asyncOffTime;

                    //频段
                    if (Reader.Region.UNSPEC == (Reader.Region)r.ParamGet("/reader/region/id"))
                    {
                        Reader.Region[] supportedRegions = (Reader.Region[])r.ParamGet("/reader/region/supportedRegions");
                        if (supportedRegions.Length < 1)
                        {
                            throw new Exception("Reader doesn't support any regions");
                        }
                        else
                        {
                            r.ParamSet("/reader/region/id", supportedRegions[0]);
                            //                        r.paramSet("/reader/region/id", Reader.Region.NA);
                        }
                    }

                    model = r.ParamGet("/reader/version/model").ToString();
                    Console.WriteLine("model=" + model);

                    //波特率设置
                    int baudRate = 115200;
                    r.ParamSet("/reader/baudRate", baudRate);
                    Console.WriteLine("baudRate=" + baudRate);

                    asyncOnTime = 1000;
                    asyncOffTime = 0;
                    r.ParamSet("/reader/read/asyncOnTime", asyncOnTime);
                    r.ParamSet("/reader/read/asyncOffTime", asyncOffTime);

                    

                    r.ParamSet("/reader/gen2/BLF", Gen2.LinkFrequency.LINK250KHZ);
                    r.ParamSet("/reader/gen2/tari", Gen2.Tari.TARI_6_25US);
                    r.ParamSet("/reader/gen2/target", Gen2.Target.A);
                    r.ParamSet("/reader/gen2/tagEncoding", Gen2.TagEncoding.M4);
                    r.ParamSet("/reader/gen2/session", Gen2.Session.S0);
                    r.ParamSet("/reader/gen2/q", new Gen2.DynamicQ());

                    int readPower = 500;
                    int writePower = 2000;
                    r.ParamSet("/reader/radio/readPower", readPower);
                    r.ParamSet("/reader/radio/writePower", writePower);
                    Console.WriteLine("gen2 and power settings success");

                    Boolean gpiTriggerRead = true;
                    Boolean autonomousRead = true;

                    //设置触发的gpi
                    if (gpiTriggerRead)
                    {
                        //GpiPinTrigger gpiPinTrigger = new GpiPinTrigger();
                        int[] gpiPin = new int[1];
                        gpiPin[0] = 1; //gpi1触发
                        gpiPinTrigger.enable = true;
                        try
                        {
                            r.ParamSet("/reader/read/trigger/gpi", gpiPin);
                        }
                        catch (Exception ex)
                        {
                            Onlog(ex);
                        }
                    }
                    

                    SimpleReadPlan srp = null;
                    srp = new SimpleReadPlan(antennlist, TagProtocol.GEN2, null, Op, false);

                    //自动读卡
                    if (autonomousRead)
                    {
                        srp.enableAutonomousRead = true;
                        Console.WriteLine("enableAutonomousRead ...");
                    }

                    //触发读卡
                    if (gpiTriggerRead)
                    {
                        srp.ReadTrigger = gpiPinTrigger;
                        Console.WriteLine("enableAutonomousRead and triggerRead ...");
                    }

                    if (autonomousRead || gpiTriggerRead)
                    {
                        r.ParamSet("/reader/read/plan", srp);
                        Console.WriteLine("readplan settings success.");

                        try
                        {
                            r.ParamSet("/reader/userConfig", new SerialReader.UserConfigOp(SerialReader.UserConfigOperation.SAVEWITHREADPLAN));
                            Console.WriteLine("User profile set option:save all configuration with read plan");

                            r.ParamSet("/reader/userConfig", new SerialReader.UserConfigOp(SerialReader.UserConfigOperation.RESTORE));
                            Console.WriteLine("User profile set option:restore all configuration");

                            isAutonomousReadStarted = true;

                            StartRead();
                            r.ReceiveAutonomousReading();

                            Thread.Sleep(3 * 1000);
                            Console.WriteLine("read done ...");
                        }
                        catch (Exception ex)
                        {
                            Onlog(ex);
                        }
                    }
                    else
                    {
                        if (antennlist.Length != 0)
                        {
                            srp.enableAutonomousRead = false;
                            srp.Antennas = antennlist;
                            r.ParamSet("/reader/read/plan", srp);
                            r.ParamSet("/reader/userConfig", new SerialReader.UserConfigOp(SerialReader.UserConfigOperation.SAVEWITHREADPLAN));
                            Console.WriteLine("success", "Configurations applied successfully\n");
                        }
                        else
                        {
                            Console.WriteLine("error: " + "Please select atleast one antenna to save the configurations with autonomous read enabled or disabled\n");
                        }
                    }
                }
                catch (Exception re)
                {
                    Onlog(re);
                }
            }
        }

        private static void StartRead()
        {
            // Create and add tag listener
            r.TagRead += MyTagRead;
            // Create and add read exception listener
            r.ReadException += new EventHandler<ReaderExceptionEventArgs>(r_ReadException);

            
        }

        private static void MyTagRead(object sender, TagReadDataEventArgs e)
        {
            Console.WriteLine("EPC: " + e.TagReadData.EpcString + ", " + e.TagReadData.Time.ToString("yyyy/MM/dd HH:mm:ss:ffff dddd"));
        }

        private static void r_ReadException(object sender, ReaderExceptionEventArgs e)
        {
            Console.WriteLine("Error: " + e.ReaderException.Message);
        }

        private static void connect()
        {
            try
            {
                //Starts the reader from default state
                if (r != null)
                {
                    r.Destroy();
                    Console.WriteLine("Reader is already exist ... ");
                }
            }
            catch (Exception ex) { Onlog(ex); }


            if (uri.StartsWith("tcp"))
            {
                Reader.SetSerialTransport("tcp", SerialTransportTCP.CreateSerialReader);
            }

            r = Reader.Create(uri);
            Console.WriteLine("create " + uri + " success");

            try
            {
                //        	r.paramSet("/reader/baudRate", 115200);
                r.Connect();
                isConnected = true;
                Console.WriteLine("normal@connect success");
            }
            catch (Exception e)
            {
                Onlog(e);
                reConnectReader();
            }
        }

        private static void reConnectReader()
        {
            int retryCount;
            for (retryCount = 1; retryCount < 6; retryCount++)
            {
                try
                {
                    r = Reader.Create(string.Concat("tmr://", uri));
                    Onlog("Attempting to reconnect - " + (retryCount) + ", after connection lost");
                    r.Connect();
                    break;
                }
                catch (Exception ex)
                {
                    Onlog(ex);
                    continue;
                }
            }

            if (retryCount >= 5)
            {
                if (r != null)
                {
                    r.Destroy();
                    r = null;
                    Onlog("[" + uri.ToString() + "]: " + "Reader reConnected Failed.");
                }
            }
            else
            {
                Onlog("[" + uri.ToString() + "]: " + "Reader reConnected Successfully.");
            }
        }

        private static void Onlog(string msg)
        {
            Console.WriteLine(msg);
        }

        private static void Onlog(Exception ex)
        {
            Console.WriteLine(ex.Message);
            Console.WriteLine(ex.StackTrace);
        }
    }
    
}

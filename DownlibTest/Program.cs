using System;
using System.Collections.Generic;
using DownstreamLib;
using System.Diagnostics;

namespace DownlibTest
{
    class Program
    {
        static void Main(string[] args)
        {
            int count = 0;
            string input = "Iron Maiden";

            Stopwatch sw = Stopwatch.StartNew();

            input = ToolsClass.FormatInputString(input);
            List<Track> listContent = Downstream.GenerateTrackList(input, -1, false, false);

            for(int i=listContent.Count-1; i>=0; i--)
            {
                Track trk = listContent[i];

                if (trk != null)
                {
                    count++;
                    Console.Write("\n\nInformation for: " + trk.Link 
                                    + "\n\tMP3 Title: " + trk.Title 
                                    + "\n\tMP3 Artist: " + trk.Artist 
                                    + "\n\tMP3 Album: " + trk.Album
                                    + "\n\tMP3 RC: " + trk.RelevenceCount);
                }
            }

            Console.WriteLine("\n\nDone.\nRecieved "+count+" out of "+listContent.Count);
            sw.Stop();
            Console.WriteLine("Time Used: {0}", sw.Elapsed.TotalSeconds);
            Console.ReadLine();
        }
    }
}
 
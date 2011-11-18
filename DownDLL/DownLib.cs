using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.IO;
using System.Threading.Tasks;
using System.Diagnostics;

namespace DownstreamLib
{
    /// <summary>
    /// Track Data class
    /// Stores common information required for each entry.
    /// </summary>
    public class Track
    {
        private string title, artist, album, link;
        private int relevenceCount;

        public string Title
        {
            get { return title; }
            set { title = value; }
        }

        public string Artist
        {
            get { return artist; }
            set { artist = value; }
        }

        public string Album
        {
            get { return album; }
            set { album = value; }
        }

        public string Link
        {
            get { return link; }
            set { link = value; }
        }

        public int RelevenceCount
        {
            get { return relevenceCount; }
            set { relevenceCount = value; }
        }


        public string[] TrackItems
        {
            get { return new string[3]{ title, artist, album }; }
            set { title = value[0]; artist = value[1]; album = value[2]; }
        }

        public Track() { }
        public Track(String title, String artist, string album, String link)
        {
            Title = title; Artist = artist; Album = album; Link = link;
        }
    };

    /// <summary>
    /// A clean interface for programmers to command the use of DownstreamLib without jumping
    /// through the hoops of using all the classes manually.
    /// </summary>
    public class Downstream
    {
        /// <summary>
        /// Generates the track listing automatically rather than calling all methods yourself.  
        /// </summary>
        /// <param name="strSearchTerm">The search term from the User.</param>
        /// <param name="targetcount">The target amount of Track(s) to return. Set to -1 to get all. (25 is recommended override)</param>
        /// <param name="removeDuplicates">Whether DownstreamLib should remove duplicate tracks.</param>
        /// <returns>A List of Track objects</returns>
        public static List<Track> GenerateTrackList(string strSearchTerm, int targetcount, bool removeDuplicates, bool focusForEfficiency)
        {
            //Handle targetcount:
            if (targetcount == -1) { targetcount = int.MaxValue; }

            List<String> data = DownloadClass.GenerateLinkList(strSearchTerm);
            List<Track> trackList = ID3Class.DownloadID3(data, targetcount);

            if (removeDuplicates)
            {
                //Remove Duplicates if true
                Dictionary<string, int> uniqueTrackDictionary = new Dictionary<string, int>();
                List<Track> noDupeTrackList = new List<Track>();

                foreach (Track trk in trackList)
                {
                    if (!uniqueTrackDictionary.ContainsKey(trk.Title))
                    {
                        uniqueTrackDictionary.Add(trk.Title, 0);
                        noDupeTrackList.Add(trk);
                    }
                }
                trackList = noDupeTrackList;
            }

            if (focusForEfficiency)
            {
                string[] queryItems = strSearchTerm.Split('-');
                List<Track> sortedList = new List<Track>();
                //We're focusing on Efficiency of results over Speed.
                foreach (Track trk in trackList)
                {
                    foreach (String trkItem in trk.TrackItems)
                    {
                        foreach (string qs in queryItems)
                        {
                            if (trkItem.IndexOf(qs, 0, trkItem.Length) != -1)
                            {
                                trk.RelevenceCount++;
                            }
                        }
                    }
                    Console.WriteLine("Relevence: " + trk.RelevenceCount);
                }

                //Organize the tracklist based on Relevence Count
                ToolsClass.TrackInsertionSort(trackList, 0, trackList.Count - 1);
            }

            return trackList;
        }
    }

    /// <summary>
    /// DownloadClass hold methods required for HTTP transfer of data.
    /// </summary>
    public class DownloadClass
    {
        /* To Add A Provider:                                        *
         * Add string to array as standard.                          *
         * Replace the search term with %@ characters as shown below */
        /* TODO: Add interface to add to these, (i.e a mutable list) */
        private static String[] ProviderList = { /* EXAMPLES OF FORMAT: */
                                                   "http://www.wuzam.com/mp3/%@-mp3-download?page=1",
                                                   "http://mp3skull.com/mp3/%@.html"
                                               };

        /// <summary>
        /// Generates a List<string> object of mp3 links from the Providers on the ProviderList
        /// </summary>
        /// <param name="strSearchTerm">The search term from the user's input</param>
        /// <returns>The List object of links</returns>
        public static List<String> GenerateLinkList(string strSearchTerm)
        {
            //TODO: Change this to List<URI>, validate URI here as well.
            List<String> listCPList = new List<string>();

            Parallel.ForEach(ProviderList, (string strSearchProvider) =>
            {
                string strSP = strSearchProvider.Replace("%@", strSearchTerm);
                try
                {
                    WebRequest webRequest = (HttpWebRequest)WebRequest.Create(strSP);
                    using (var webResponse = webRequest.GetResponse().GetResponseStream())
                    {
                        StreamReader responseReader = new StreamReader(webResponse);

                        while (webResponse != null)
                        {
                            String webString = responseReader.ReadLine();
                            if (webString == null) { break; }

                            if (webString.IndexOf(".mp3", 0, webString.Length) != -1)
                            {
                                int startIndex = 0, endIndex = 0;
                                endIndex = 4 + webString.IndexOf(".mp3", 0, webString.Length);

                                for (int i = (endIndex - 4); i > 0; i--)
                                {
                                    string identifier = webString.Substring(i, 4).ToLower();
                                    if (identifier.IndexOf("http", 0, identifier.Length) != -1)
                                    {
                                        startIndex = i;
                                        break;
                                    }
                                }

                                string webLink = webString.Substring(startIndex, (endIndex - startIndex));

                                if (!listCPList.Contains(webLink))
                                    listCPList.Add(webLink);
                            }
                        }
                    }
                }
                catch (WebException ex) { Console.WriteLine("WebException Caught: Base: GenerateLinkList().\n"+ex.Message); }
            }); //END System.Threading.Tasks.Parallel.ForEach

            //Should now have a list of links from each provider, next: check each length and weed out <30s ones.
            return listCPList;
        }
    }

    /// <summary>
    /// ID3Class hold methods to interpret ID3 tags to generate info about the tracks.
    /// </summary>
    public class ID3Class
    {
        private static System.Text.UTF7Encoding encoder = new UTF7Encoding();
        /// <summary>
        /// Downloads the ID3 tag data from a given URI
        /// Runs Syncronously, so should preferably used in conjun. with BackgroundWorker.DoWork()
        /// </summary>
        /// <param name="targetString">The target strings to download from.</param>
        /// <returns>An array of all data.</returns>
        public static List<Track> DownloadID3(List<string> targetStrings, int targetcount)
        {
            int bytecount = 1024;
            List<Track> TrackObjects = new List<Track>();

            Parallel.ForEach(targetStrings, (string targetString, ParallelLoopState loopState) =>
            {
                Track currentTrack = new Track();
                WebRequest m_WebRequestHEAD = null;
                WebRequest m_WebRequest = null;

                try
                {
                    m_WebRequestHEAD = (HttpWebRequest)WebRequest.Create(targetString);
                    m_WebRequestHEAD.Method = "HEAD";
                    m_WebRequestHEAD.Timeout = 2000;

                    m_WebRequest = (HttpWebRequest)WebRequest.Create(targetString);
                    m_WebRequest.Timeout = 2000;
                }
                catch (WebException ex) { Console.Write("WebException Caught: " + ex.Message); return; }
                catch (UriFormatException ufi) { Console.Write("UriFormatException Caught: " + ufi.Message); return; }
                                                                                                              
                WebResponse wResponseHEAD = null;                                                                    
                try { wResponseHEAD = m_WebRequestHEAD.GetResponse(); }
                catch (WebException ex) { Console.WriteLine("WebException Caught: m_WebRequestHEAD.GetResponse()\n"+ex.Message); return; }


                // Parse the content length to check for 'preview' or 'demo' files.
                int cLen = 0;
                if ((wResponseHEAD != null) && int.TryParse(wResponseHEAD.Headers.Get("Content-Length"), out cLen))
                { if (cLen < 1500000) { return; } }
                wResponseHEAD.Close();

                WebResponse wResponse = null;
                try { wResponse = m_WebRequest.GetResponse(); }
                catch (Exception ex) { Console.WriteLine("Exception Caught: m_WebRequest.GetResponse()\n"+ex.Message); return; }

                using (var m_WebResponse = m_WebRequest.GetResponse().GetResponseStream())
                {
                    byte[] buffer = new byte[bytecount];
                    m_WebResponse.Read(buffer, 0, bytecount);

                    //Get the ID3 Track Info
                    currentTrack = ParseID3(buffer);

                    if ((currentTrack != null) && (!currentTrack.Title.StartsWith("??")))
                    {
                        currentTrack.Link = targetString;
                        TrackObjects.Add(currentTrack);
                    }
                }
            });
            return TrackObjects;
        }

        /// <summary>
        /// Tries to parse the frame data for a given frame, then returns the string representation of the data.
        /// </summary>
        /// <param name="buffer">ID3 buffer from the Stream.</param>
        /// <param name="frameData">Current frame's data.</param>
        /// <param name="count">Current count in the buffer.</param>
        /// <returns></returns>
        private static string TryParseFrameData(byte[] buffer, uint count, uint frameSize)
        {
            try
            {
                byte[] bytes_FrameData = new byte[frameSize];
                Array.Copy(buffer, (count + 10), bytes_FrameData, 0, frameSize);

                string FrameDataAsString = encoder.GetString(bytes_FrameData).Replace("\0", "")
                                                                             .Replace("??", "")
                                                                             .Replace("\x01", "")
                                                                             .Replace("ÿþ", "");
                return FrameDataAsString;
            }
            catch (Exception ex)
            {
                Console.WriteLine("TryParseFrameData: " + ex.Message);
                return null;
            }
        }

        /// <summary>
        /// Parses a given buffer of ID3 information into a readable Track object.
        /// Called once per MP3 link.
        /// </summary>
        /// <param name="buffer">Data from the download process of the ID3 Tag</param>
        /// <returns>Full Track object, either with data or NULL if error.</returns>
        private static Track ParseID3(byte[] buffer)
        {
            Track m_Track = new Track();

            try
            {
                byte[] bytes_ID3 = new byte[3];
                Array.Copy(buffer, 0, bytes_ID3, 0, bytes_ID3.Length);
                string ID3_Identifier = encoder.GetString(bytes_ID3);

                if (ID3_Identifier == "ID3") //Basic Validation for misaligned/misformed headers.
                {
                    uint count = 10;
                    while ((count+8) < buffer.Length)
                    {
                        byte[] frameData = new byte[8];
                        Array.Copy(buffer, count, frameData, 0, frameData.Length);

                        byte[] bytes_FrameID = new byte[4];
                        Array.Copy(frameData, 0, bytes_FrameID, 0, 4);
                        string frameID = encoder.GetString(bytes_FrameID).Trim('\0'); //GET THE FRAME NAME:

                        byte[] bytes_RawFrameSize = new byte[4];
                        Array.Copy(frameData, 4, bytes_RawFrameSize, 0, bytes_RawFrameSize.Length);

                        if (BitConverter.IsLittleEndian)
                            Array.Reverse(bytes_RawFrameSize);

                        uint frameSize = BitConverter.ToUInt32(bytes_RawFrameSize, 0);

                        if (frameID.Equals("TALB")) //Track Album Name
                        {
                            m_Track.Album = TryParseFrameData(buffer, count, frameSize);
                        }
                        else if (frameID.Equals("TIT1") || frameID.Equals("TIT2")) //Track Title
                        {
                            m_Track.Title = TryParseFrameData(buffer, count, frameSize);
                        }
                        else if (frameID.Equals("TPE1") || frameID.Equals("TPE2")) //Track Artist
                        {
                            m_Track.Artist = TryParseFrameData(buffer, count, frameSize);
                        }

                        //Increase count to the start of the next frame.
                        count += (frameSize + 10);

                        if (!(string.IsNullOrEmpty(m_Track.Album)) && !(string.IsNullOrEmpty(m_Track.Artist)) && !(string.IsNullOrEmpty(m_Track.Title)))
                        {
                            //EVERYTHING IS FULL.
                            return m_Track;
                        }
                    }
                    return null; //Completely parsed the buffer, nothing gained, failed? Yes.
                }
                else return null; //No valid ID3 Header Magic found. Failed.
            }
            catch (Exception ex)
            { Console.Write("ParseID3: {0}\n", ex.Message); return null; }  //Just flat-out failed, log the reason for later fixing.
        }
    }

    /// <summary>
    /// Tools Class. Do not use externally, unless required.
    /// Or do. Do what you want, I'm a Summary, not a cop.
    /// </summary>
    public class ToolsClass
    {
        /// <summary>
        /// Formats the input string from the GUI Textbox "Search Box" into a HTTP-usable form.
        /// </summary>
        /// <param name="str">input string</param>
        /// <returns>A viable string for use on the web</returns>
        public static string FormatInputString(string str)
        {
            char[] broken_chars = new char[] { '\'', '"', '.', '\\', '/', '\'', ',' };

            str = str.Replace(' ', '-');
            foreach (char c in broken_chars)
            {
                str = str.Replace(c, '\0');
            }

            return str;
        }

        public static void TrackInsertionSort(List<Track> usList, int start, int end)
        {
            int i = start;
            int j = end;
            if (end - start >= 1)
            {
                int pivot = usList[start].RelevenceCount;

                while (j > i)
                {
                    while (usList[i].RelevenceCount <= pivot && i <= end && j > i)
                        i++;
                    while (usList[j].RelevenceCount > pivot && j >= start && j >= i)
                        j--;

                    if (j > i) SwapTrack(usList, i, j);
                }
                SwapTrack(usList, i, end);

                TrackInsertionSort(usList, start, j - 1);
                TrackInsertionSort(usList, j + 1, end);
            }
        }

        private static void SwapTrack(List<Track> usList, int trackA, int trackB)
        {
            Track tmp = usList[trackA];
            usList[trackA] = usList[trackB];
            usList[trackB] = tmp;    
        }


        /* Syncsafe will be added if performance is increased to above expectations. *
         * Implementing ~125 calls to this will detriment performance.       */
        public static int synchsafe(int inData)
        {
            int outData = 0, mask = 0x7F;

            while ((mask ^ 0x7FFFFFFF) != 0)
            {
                outData = inData & ~mask;
                outData <<= 1;
                outData |= inData & mask;
                mask = ((mask + 1) << 8) - 1;
                inData = outData;
            }

            return outData;
        }
    }
}
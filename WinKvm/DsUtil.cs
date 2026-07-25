using DirectShowLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace WinKvm
{
    public class DsUtil
    {
        /// <summary>
        /// DsDeviceから利用可能な解像度を取得する
        /// </summary>
        public static List<Resolution> GetAllAvailableResolution(DsDevice vidDev)
        {
            if (new FilterGraph() is not IFilterGraph2 m_FilterGraph2)
            {
                throw new Exception("Could not create FilterGraph2");
            }

            var hr = m_FilterGraph2.AddSourceFilterForMoniker(vidDev.Mon, null, vidDev.Name, out IBaseFilter sourceFilter);
            DsError.ThrowExceptionForHR(hr);

            var pRaw2 = DsFindPin.ByCategory(sourceFilter, PinCategory.Capture, 0);
            var AvailableResolutions = new List<Resolution>();

            VideoInfoHeader v = new();
            hr = pRaw2.EnumMediaTypes(out IEnumMediaTypes mediaTypeEnum);
            DsError.ThrowExceptionForHR(hr);

            AMMediaType[] mediaTypes = new AMMediaType[1];
            IntPtr fetched = IntPtr.Zero;
            while (mediaTypeEnum.Next(1, mediaTypes, fetched) == 0)
            {
                if (mediaTypes[0].formatType != DirectShowLib.FormatType.VideoInfo) continue;
                Marshal.PtrToStructure(mediaTypes[0].formatPtr, v);
                if (v.BmiHeader.Size != 0 && v.BmiHeader.BitCount != 0)
                    AvailableResolutions.Add(
                        new Resolution(v.BmiHeader.Height, v.BmiHeader.Width, GuidToFourCc(mediaTypes[0].subType),
                        (int)(100000000 / v.AvgTimePerFrame) /10.0));
            }
            return AvailableResolutions;
        }

        /// <summary>
        /// GUIDをFourCCに変換する
        /// </summary>
        public static string GuidToFourCc(Guid guid)
        {
            if (guid == MediaSubType.MJPG)
            {
                return "MJPG";
            }
            else if (guid == MediaSubType.YUY2)
            {
                return "YUY2";
            }
            else if (guid == MediaSubType.NV12)
            {
                return "NV12";
            }
            else if (guid == MediaSubType.I420)
            {
                return "I420";
            }
            else
            {
                return "    ";
            }
        }
    }

    /// <summary>
    /// 解像度を表すクラス
    /// </summary>
    public class Resolution
    {
        public int Height { get; set; }
        public int Width { get; set; }
        public string FourCc { get; set; }
        public double Fps { get; set; }

        public Resolution(int height, int width, string fourCc, double fps)
        {
            Height = height;
            Width = width;
            FourCc = fourCc;
            Fps = fps;
        }
    }
}


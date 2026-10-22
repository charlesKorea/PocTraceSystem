using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Serialization.Formatters.Binary;
using System.Net.NetworkInformation;
using System.Security.Cryptography;

using System.Data;

// [Refactor] WinForms/Drawing 의존(MessageBox, Dialog, DataGridView 등) 레거시 코드는
// Core를 UI 프레임워크로부터 분리하기 위해 제거되었습니다. (사용처 없음 확인)
using DataTable = System.Data.DataTable;
using Serilog;
using Serilog.Core;

using PocTrace.Core.Models;

namespace PocTrace.Core.Utils
{
    public class Util
    {
        private readonly ILogger _logger;

        public Util(ILogger logger)
        {
            _logger = logger;
        }

        public Util() { }

        //전역변수 선언
        public BinaryFormatter binaryFormatter = new BinaryFormatter();
        //private X.Application m_excel = null;

        //엑셀데이터 저장 list
        List<ExcelFileInfo> m_filelist = new List<ExcelFileInfo>();

        //모든 자체 제작 프로그램 내 기간 제한 설정 용도
        public static int expireYear = 2026;
        public static int expireMonth = 12;
        public static int expireDate = 31;

        //ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

        private const string hash = "J-Labs2024SamsungSDS";

        public string MD5HashDecypt(string val)
        {
            byte[] data = Convert.FromBase64String(val);
            using (MD5CryptoServiceProvider md5 = new MD5CryptoServiceProvider())
            {
                byte[] keys = md5.ComputeHash(UTF8Encoding.UTF8.GetBytes(hash));
                using (TripleDESCryptoServiceProvider tripDes = new TripleDESCryptoServiceProvider() { Key = keys, Mode = CipherMode.ECB, Padding = PaddingMode.PKCS7 })
                {
                    ICryptoTransform transform = tripDes.CreateDecryptor();
                    byte[] results = transform.TransformFinalBlock(data, 0, data.Length);
                    return UTF8Encoding.UTF8.GetString(results);
                }
            }
        }

        public string MD5HashEncrypt(string val)
        {
            byte[] data = UTF8Encoding.UTF8.GetBytes(val);
            using (MD5CryptoServiceProvider md5 = new MD5CryptoServiceProvider())
            {
                byte[] keys = md5.ComputeHash(UTF8Encoding.UTF8.GetBytes(hash));
                using (TripleDESCryptoServiceProvider tripDes = new TripleDESCryptoServiceProvider() { Key = keys, Mode = CipherMode.ECB, Padding = PaddingMode.PKCS7 })
                {
                    ICryptoTransform transform = tripDes.CreateEncryptor();
                    byte[] results = transform.TransformFinalBlock(data, 0, data.Length);
                    return Convert.ToBase64String(results, 0, results.Length);
                }
            }
        }

        public string GetLocalMacAddress()
        {
            return NetworkInterface.GetAllNetworkInterfaces().Where(nic => nic.OperationalStatus == OperationalStatus.Up && nic.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .Select(nic => nic.GetPhysicalAddress().ToString()).FirstOrDefault();
        }

        // object casting 방법
        /*
         * m_utList = objList.Cast<UTMapping>().ToList(); (object -> custom class)
         * list.Cast<object>().ToList() (custom class -> object)
         */

        public bool IsExpireDateCheck()
        {
            DateTime today = System.DateTime.Today;
            DateTime endday = new DateTime(expireYear, expireMonth, expireDate, 23, 59, 59);
            TimeSpan dayDiff = endday - today;  // 시간의 차이를 구할 때 TimeSpan 사용

            if (dayDiff.TotalDays < 0)
                return true;

            return false;
        }

        public string GetPathFromFilePath(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;

            return path.Substring(0, path.LastIndexOf("\\"));
        }

        public DateTime GetBuildDateTime(System.Version version = null)
        {
            if (version == null)
                version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;

            int day = version.Build;
            DateTime dtBuild = (new DateTime(2000, 1, 1)).AddDays(day);

            //int sec = version.Revision;

            return dtBuild;
        }

        public string GetDateFromString(string org)
        {
            if (string.IsNullOrEmpty(org))
                return org;

            if (DateTime.TryParse(org, out DateTime dt))
            {
                return dt.ToShortDateString();
            }

            return org;
        }

        public void ReleaseExcelObject(object obj)
        {
            try
            {
                if (obj != null)
                {
                    Marshal.ReleaseComObject(obj);
                    obj = null;
                }
            }
            catch (Exception ex)
            {
                obj = null;
                throw ex;
            }
            finally
            {
                GC.GetTotalMemory(false);
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                GC.GetTotalMemory(true);
            }
        }

        public string ConvertDataRowToString(object o)
        {
            if (string.IsNullOrEmpty(o.ToString()))
                return "-";
            else
                return o.ToString();
        }

        public int ConvertDataRowToInt(object o)
        {
            if (string.IsNullOrEmpty(o.ToString()))
                return -1;
            else
                return Convert.ToInt32(o.ToString());
        }

        public double ConvertDataRowToDouble(object o)
        {
            if (string.IsNullOrEmpty(o.ToString()))
                return -1;
            else
                return Convert.ToDouble(o.ToString());
        }

        public double SetRoundDoube(double value)
        {
            return (double)Math.Round(value, 2);
        }

        public string SetNumberFormat(int n)
        {
            if (n == 0)
                return "0";
            return string.Format("{0:#,###}", n);
        }

        public string SetNumberFormat(double n)
        {
            if (n == 0)
                return "0";
            return string.Format("{0:#,###}", n);
        }

        public string DecToAlphabet(int num)
        {
            int rest; //나눗셈 계산에 사용될 나머지 값
            string alphabet; //10진수에서 알파벳으로 변환될 값

            byte[] asciiA = Encoding.ASCII.GetBytes("A"); // 0=>A
            rest = num % 26; // A~Z 26자
            asciiA[0] += (byte)rest; // num 0일 때 A, num 4일 때 A+4 => E

            alphabet = Encoding.ASCII.GetString(asciiA); //변환된 알파벳 저장

            num = num / 26 - 1; // 그 다음 자리의 알파벳 계산을 재귀하기 위해, 받은 수/알파벳수 -1 (0은 A라는 문자값이 있으므로 -1을 기준으로 계산함)
            if (num > -1)
            {
                alphabet = alphabet.Insert(0, DecToAlphabet(num)); //재귀 호출하며 결과를 앞자리에 insert
            }
            return alphabet; // 최종값 return
        }

        public bool IsStringContains(string source, string target)
        {
            if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(target))
                return false;

            if (source.IndexOf(target, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            return false;
        }

        public string SettingObject2String(object o)
        {
            if (o == null)
                return string.Empty;

            string str = o.ToString();

            if (string.IsNullOrEmpty(str) || string.IsNullOrWhiteSpace(str) || str.Equals("-"))
                return string.Empty;

            return str;
        }

        public string GetSingleStrValue(string str, int index)
        {
            string var = "-";

            if (index == 0 || string.IsNullOrEmpty(str))
                var = "-";
            else
                var = str;

            return var;
        }

        public double GetSingleDoubleValue(string str)
        {
            double dw = -1;

            if (string.IsNullOrEmpty(str))
                dw = -1;
            else
                dw = Math.Round(Double.Parse(str), 3);

            return dw;
        }

        public string GetConvertedHUTableNameFromSubDiv2(string subDiv2)
        {
            //pump infra -> pump
            var subDivForDBTableName = subDiv2;
            if (IsStringContains(subDiv2, "pump"))
                return "PUMP";

            return subDivForDBTableName;
        }

        public int CountCharInString(string str, string word)
        {
            string[] StringArray = str.Split(new string[] { word }, StringSplitOptions.None);

            return StringArray.Length - 1;
        }

        public int GetWeekNumberOfToday(DateTime date)
        {
            System.Globalization.CultureInfo culture = System.Globalization.CultureInfo.CurrentCulture;

            System.Globalization.CalendarWeekRule weekRule = culture.DateTimeFormat.CalendarWeekRule;
            DayOfWeek firstDayOfWeek = culture.DateTimeFormat.FirstDayOfWeek;
            int WeekOfYear = culture.Calendar.GetWeekOfYear(date, weekRule, firstDayOfWeek);

            return WeekOfYear;
        }

        public DateTime GetFirstDateOfWeek(int year, int week)
        {
            DateTime firstDateOfYear = new DateTime(year, 1, 1);
            DateTime firstDateOfFirstWeek = firstDateOfYear.AddDays(7 - (int)(firstDateOfYear.DayOfWeek) + 1);

            return firstDateOfFirstWeek.AddDays(7 * (week - 2));
        }

        public int GetUsedUTCount(List<RAWDATA_REQ_EXCEL> connectorReport, bool fMain = false, string mainEQPName = "")
        {
            int count = 0;

            if (connectorReport == null || connectorReport.Count < 5)
                return -1;

            if (fMain)
                count = connectorReport.FindAll(x => !string.IsNullOrEmpty(mainEQPName) &&
                                                  x.sName.Equals(mainEQPName) &&
                                                  !string.IsNullOrEmpty(x.sUT)).Select(x => x.sUT).Distinct().Count();
            else
                count = connectorReport.FindAll(x => !string.IsNullOrEmpty(x.sUT)).Select(x => x.sUT).Distinct().Count();

            return count;
        }

        public int GetModelingPipeCount(List<RAWDATA_REQ_EXCEL> connectorReport, bool fMain = false, string mainEQPName = "")
        {
            int Count = 0;

            if (connectorReport == null || connectorReport.Count < 5)
                return -1;

            //Status EMP, ING는 미설계로 제외
            var validPipeList = connectorReport.FindAll(x => !string.IsNullOrEmpty(x.status)
                                                             && (!x.status.Equals("EMP") && !x.status.Equals("ING")));

            //Main설비의 경우 list를 재정의
            if (fMain && validPipeList != null && !string.IsNullOrEmpty(mainEQPName))
                validPipeList = validPipeList.FindAll(x => x.sName.Equals(mainEQPName));

            //GUID 기반으로 배관 설계 확인
            var duplicatedPipeList = validPipeList.FindAll(x => x.status.Equals("EQP") && x.sTag.Equals(x.tTag));
            int duplicatedPipeCount = 0;
            if (duplicatedPipeList != null)
            {
                foreach (var pipe in duplicatedPipeList)
                {
                    //S:GUID-T:GUID가 pair로 반대로 있는 경우는 중복 수량
                    if (duplicatedPipeList.Exists(x => !string.IsNullOrEmpty(x.tGUID) && !string.IsNullOrEmpty(x.sGUID)
                                                        && x.tGUID.Equals(pipe.sGUID) && x.sGUID.Equals(pipe.tGUID)))
                        duplicatedPipeCount++;
                }
            }

            if (validPipeList != null)
            {
                Count = validPipeList.Count - duplicatedPipeCount;
            }
            else
                Count = 0; //오류

            return Count;
        }

        public int GetEquipmentPocCount(List<RAWDATA_REQ_EXCEL> connectorReport, bool fMain = false, string mainEQPName = "")
        {
            if (connectorReport == null || connectorReport.Count < 5)
                return -1;

            if (fMain && !string.IsNullOrEmpty(mainEQPName))
                return connectorReport.FindAll(x => x.sName.Equals(mainEQPName)).Select(x => x.cGUID).Distinct().Count();
            else
                return connectorReport.Select(x => x.cGUID).Distinct().Count();
        }

        public int GetNonModelingPocCount(List<RAWDATA_REQ_EXCEL> connectorReport, bool fMain = false, string mainEQPName = "")
        {
            if (connectorReport == null || connectorReport.Count < 5)
                return -1;

            //Status EMP, ING는 미설계로 제외
            int nonModelingPocCount = 0;

            if (fMain && !string.IsNullOrEmpty(mainEQPName))
                nonModelingPocCount = connectorReport.FindAll(x => x.sName.Equals(mainEQPName)
                                                             && !string.IsNullOrEmpty(x.status)
                                                             && (x.status.Equals("EMP") || x.status.Equals("ING"))
                                                             && (!string.IsNullOrEmpty(x.sUTGroup) && !IsStringContains(x.sUTGroup, "ELECTRIC"))
                                                             && !IsStringContains(x.connectorName, "power") //power 제외
                                                             && !IsStringContains(x.tName, "open") //open pipe 제외
                                                             && !IsStringContains(x.connectorName, "미사용") //미사용 제외
                                                             && !IsStringContains(x.connectorUse, "n") //connector use N 제외
                                                             && !IsStringContains(x.connectorName, "3차") //3차 제외
                                                             ).Count;
            else
                nonModelingPocCount = connectorReport.FindAll(x => !string.IsNullOrEmpty(x.status)
                                                             && (x.status.Equals("EMP") || x.status.Equals("ING"))
                                                             && (!string.IsNullOrEmpty(x.sUTGroup) && !IsStringContains(x.sUTGroup, "ELECTRIC"))
                                                             && !IsStringContains(x.connectorName, "power") //power 제외
                                                             && !IsStringContains(x.tName, "open") //open pipe 제외
                                                             && !IsStringContains(x.connectorName, "미사용") //미사용 제외
                                                             && !IsStringContains(x.connectorUse, "n") //connector use N 제외
                                                             && !IsStringContains(x.connectorName, "3차") //3차 제외
                                                             ).Count;

            return nonModelingPocCount;
        }

        public int GetIntPercentage(int total, int issues)
        {
            if (total == 0 || issues == 0)
                return 0;

            double value = Convert.ToDouble(issues) / Convert.ToDouble(total);
            value = Math.Round(value * 100, 1);
            return Convert.ToInt32(value);
        }

        public int GetEmptyNullDefaultInt(int n)
        {
            if (string.IsNullOrEmpty(n.ToString()))
                return -1;

            return n;
        }

        //24.11.13
        public string GetHUPlanFromFilename(string filename)
        {
            if (IsStringContains(filename, "신규"))
                return "신규";

            if (IsStringContains(filename, "개조"))
                return "개조";

            if (IsStringContains(filename, "재출도"))
                return "재출도";

            if (IsStringContains(filename, "계획외"))
                return "계획외";

            if (IsStringContains(filename, "이설"))
                return "이설";

            return "미기입";
        }

        public bool 예외대상이름여부(string UTname)
        {
            if (string.IsNullOrEmpty(UTname) ||
                IsStringContains(UTname, "未使用") ||
                IsStringContains(UTname, "미사용") ||
                IsStringContains(UTname, "NOT USE") ||
                IsStringContains(UTname, "NO USE") ||
                IsStringContains(UTname, "ETC") ||
                IsStringContains(UTname, "ELEC") || //24.05.14 add
                IsStringContains(UTname, "기타") || //24.05.14 add
                IsStringContains(UTname, "Maker") || //25.12.29 add
                IsStringContains(UTname, "전기") || //25.12.29 add
                IsStringContains(UTname, "spare"))
                return true;

            return false;
        }

        public string GetEQPCategory(string EQPID)
        {
            if (string.IsNullOrEmpty(EQPID)) return "-";

            string cat = "-";

            string e = EQPID.Substring(0, 1);
            switch (e)
            {
                case "D": cat = "DIFF"; break;
                case "E": cat = "ETCH"; break;
                case "I": cat = "IMP"; break;
                case "S": cat = "METAL"; break;
                case "T": cat = "CVD"; break;
                case "M": cat = "MI"; break;
                case "W": cat = "CLN"; break;
                case "P": cat = "PHOTO"; break;
                case "C": cat = "CMP"; break;
            }

            return cat;
        }

        public bool IsStringContainsOneOfList(string source, List<string> list)
        {
            foreach (var v in list)
            {
                if (IsStringContains(source, v)) return true;
            }

            return false;
        }

        public bool IsEmptyValue(string var)
        {
            if (string.IsNullOrEmpty(var) || var.Equals("-"))
                return true;

            return false;
        }

        public long GetFileSize(string filepath)
        {
            long filesize = 0;
            if (File.Exists(filepath))
            {
                FileInfo info = new FileInfo(filepath);
                filesize = info.Length;
            }

            return filesize;
        }

        public string GetFolderPathFromEntirePath(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;

            return path.Substring(0, path.LastIndexOf("\\"));
        }

        public string GetFileNameFromFilePath(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            if (path.LastIndexOf(@"\") < 1) return null;

            int FileNameStartPos = path.LastIndexOf(@"\") + 1;
            return path.Substring(FileNameStartPos);
        }

        public void SetValidSheetCheckList(List<ValidSheetCheck> list)
        {
            for (int i = 0; i < list.Count; i++)
                Log.Information("m_vsclist[" + i.ToString() + "] "
                    + " " + list[i].nRow.ToString()
                    + " " + list[i].nCol.ToString()
                    + " " + list[i].strValidValue);
        }

        public string GetEmptyNullDefaultString(string s)
        {
            if (string.IsNullOrEmpty(s))
                return "-";

            if (IsStringContains(s, "'"))
                return s.Replace("'", "''");

            return s;
        }

        public string GetShortDateStringFromDateString(string org)
        {
            DateTime dt = GetDateTimeFromString(org);
            if (DateTime.MinValue == dt)
                return "-";
            if (dt.Year <= 2010) //2010년 이하 데이터는 무시
                return "-";

            return dt.ToShortDateString();
        }

        public int GetIntFromString(string org)
        {
            int n = 0;

            if (string.IsNullOrEmpty(org))
                return 0;

            if (Int32.TryParse(org, out n))
            {
                if (n < 0) //날짜 숫자로 변환했을 때 숫자 100보다 작다면 문제가 있다
                    return 0;
            }

            return n;
        }

        public DateTime GetDateTimeFromString(string org)
        {
            if (string.IsNullOrEmpty(org))
                return DateTime.MinValue;

            //Log.Information($"GetDateTimeFromString: {org}");

            if (Int32.TryParse(org, out int i))
            {
                if (i < 100) //날짜 숫자로 변환했을 때 숫자 100보다 작다면 문제가 있다
                    return DateTime.MinValue;
            }

            if (DateTime.TryParse(org, out DateTime dt))
            {
                return dt;
            }

            return DateTime.MinValue;
        }

        public string GetLineInfo(string org)
        {
            string convert = org;

            if (IsStringContains(org, "x1"))
                convert = "X1";
            else if (IsStringContains(org, "ph1"))
                convert = "X2-1";
            else if (IsStringContains(org, "ph2"))
                convert = "X2-2";
            else if (IsStringContains(org, "x2"))
                convert = "X2L";
            else
                convert = "-";

            //if (org != convert)
            //    Log.Information($"GetLineInfo Converted! {org} To {convert}");

            return convert;
        }
        public string GetValidPartName(string part)
        {
            if (IsStringContains(part, "photo") || IsStringContains(part, "pho"))
                return "PHOTO";

            if (IsStringContains(part, "cln") || IsStringContains(part, "clean"))
                return "CLN";

            if (IsStringContains(part, "mi") || IsStringContains(part, "metro"))
                return "MI";

            if (IsStringContains(part, "etch"))
                return "ETCH";

            if (IsStringContains(part, "cmp"))
                return "CMP";

            if (IsStringContains(part, "diff"))
                return "DIFF";

            if (IsStringContains(part, "metal"))
                return "METAL";

            if (IsStringContains(part, "cvd"))
                return "CVD";

            if (IsStringContains(part, "imp"))
                return "IMP";

            return "?";
        }

        public string GetValidConstuctorName(string name)
        {
            if (IsStringContains(name, "B") || IsStringContains(name, "BNH"))
                return "BNH";

            if (IsStringContains(name, "삼건") || IsStringContains(name, "陕"))
                return "三建";

            if (IsStringContains(name, "우신") || IsStringContains(name, "宇信"))
                return "宇信ENG";

            if (IsStringContains(name, "한양전자") || IsStringContains(name, "韩阳"))
                return "韩阳电气";

            if (IsStringContains(name, "한양ENG") || IsStringContains(name, "汉旸"))
                return "汉旸ENG";

            return name;
        }

        public string GetBIMFileKey(string filename, bool fBIMGroup = false)
        {
            if (filename.IndexOf("TEN", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                //this.pjt = PJT_TYPE.A1;

                // A1의 경우            
                int FileKeyStartPos = filename.IndexOf(@"TEN_");
                return filename.Substring(FileKeyStartPos, 19);

            }
            else if (filename.IndexOf("M-PJT", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                //this.pjt = PJT_TYPE.X1;

                int length = filename.Length;
                int FileKeyStartPos = filename.IndexOf(@"M-PJT") + 6; //M-PJT_ 이후
                int FileKeyEndPos = length - 5;//.xlsx

                if (fBIMGroup)
                {
                    if (filename.IndexOf("Central", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        FileKeyEndPos = filename.IndexOf(@"_Central");
                    }
                    else if (filename.IndexOf("_22", StringComparison.OrdinalIgnoreCase) >= 0)
                    {//BIM 변환 후 그룹명 끝에 변환 날짜를 붙이는 태울..
                        FileKeyEndPos = filename.IndexOf(@"_22");
                    }
                    else if (filename.IndexOf("_21", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        FileKeyEndPos = filename.IndexOf(@"_21");
                    }
                    else if (filename.IndexOf("_20", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        FileKeyEndPos = filename.IndexOf(@"_20");
                    }
                    else if (filename.IndexOf("_19", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        FileKeyEndPos = filename.IndexOf(@"_19");
                    }
                    else if (filename.IndexOf("Central", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        FileKeyEndPos = filename.IndexOf(@"_Central");
                    }
                    else if (filename.IndexOf("Instance", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        FileKeyEndPos = filename.IndexOf(@"Instance");
                    }
                }
                else
                {
                    if (filename.IndexOf("Central", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        FileKeyEndPos = filename.IndexOf(@"_Central");
                    }
                    else if (filename.IndexOf("_JSearch", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        FileKeyEndPos = filename.IndexOf(@"_JSearch");
                    }
                    else if (filename.IndexOf("_CompareResult", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        FileKeyEndPos = filename.IndexOf(@"_CompareResult");
                    }
                }

                //CAPA 있는 경우
                if (filename.IndexOf("_CAPA", StringComparison.OrdinalIgnoreCase) >= 0)
                    FileKeyEndPos = filename.IndexOf(@"_CAPA") + 3;

                return filename.Substring(FileKeyStartPos, FileKeyEndPos - FileKeyStartPos);
            }
            else if (filename.IndexOf("X2", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                int length = filename.Length;
                int FileKeyEndPos = length - 5;//.xlsx
                // X2의 경우            
                int FileKeyStartPos = filename.IndexOf(@"X");
                if (filename.IndexOf("Central", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    FileKeyEndPos = filename.IndexOf(@"_Central");
                }
                else if (filename.IndexOf("_JSearch", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    FileKeyEndPos = filename.IndexOf(@"_JSearch");
                }
                else if (filename.IndexOf("_CompareResult", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    FileKeyEndPos = filename.IndexOf(@"_CompareResult");
                }

                return filename.Substring(FileKeyStartPos, FileKeyEndPos - FileKeyStartPos);
                //this.pjt = PJT_TYPE.X2;
            }
            else if (filename.IndexOf("P1", StringComparison.OrdinalIgnoreCase) >= 0 ||
                filename.IndexOf("P2", StringComparison.OrdinalIgnoreCase) >= 0 ||
                filename.IndexOf("P3", StringComparison.OrdinalIgnoreCase) >= 0 ||
                filename.IndexOf("V2", StringComparison.OrdinalIgnoreCase) >= 0 ||
                filename.IndexOf("E", StringComparison.OrdinalIgnoreCase) >= 0 ||
                filename.IndexOf("S3V", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                // 평택 site의 경우           
                int FileKeyStartPos = 0;
                return filename.Substring(FileKeyStartPos, 20);
                //this.pjt = PJT_TYPE.P3;
            }

            return null;
        }

        public double Convert5DMM2Double(string str)
        {
            if (string.IsNullOrEmpty(str))
                return -1;
            else
            {
                int subIndex = str.IndexOf("m") - 2;
                if (subIndex >= 0)
                {
                    string var = str.Substring(1, subIndex);
                    return Math.Round(Double.Parse(var), 0);
                }
                else
                    return -1;
            }
        }

        public void Debug_StrLoadValues_SingleSRD(SheetReadData srd)
        {
            Log.Information("Debug_StrLoadValues_SingleSRD");

            if (null == srd)
            {
                Log.Information("Null SRD");
                return;
            }

            for (int r = 0; r < srd.str_load_values.GetLength(0); r++)
            {
                string line = $"[{r}]: ";
                for (int c = 0; c < srd.str_load_values.GetLength(1); c++)
                {
                    line += $"{srd.str_load_values[r, c]}, ";
                }
                Log.Information(line);
            }
        }

        public int GetIndexOfElementInStringArray(string[] array, string value)
        {
            for (int i = 0; i < array.Length; i++)
            {
                if (array[i].Equals(value))
                {
                    return i;
                }
            }

            return -1;
        }

        public void CompareTwoSRD(SheetReadData srdA, SheetReadData srdB,
            string keyheader, int nMode,
            out string[,] write_arr, out string[,] write_arrA, out string[,] write_arrB,
            out int write_row)
        {
            write_arr = new string[5, 5];
            List<string> onlyListA = new List<string>();
            List<string> onlyListB = new List<string>();
            write_arrA = null;
            write_arrB = null;

            write_row = 0;

            if (string.IsNullOrEmpty(keyheader))
            {
                Log.Information("NULL keyheader");
                return;
            }

            int[] map = new int[srdA.str_valid_headers.Length];
            for (int i = 0; i < srdA.str_valid_headers.Length; i++)
            {
                map[i] = GetIndexOfElementInStringArray(srdB.str_valid_headers, srdA.str_valid_headers[i]);
            }
            //map[0]의 의미는 a 헤더의 0번째 값이 b 헤더의 몇 번째 헤더인지 매핑

            int keyIndexA = GetIndexOfElementInStringArray(srdA.str_valid_headers, keyheader);
            int keyIndexB = GetIndexOfElementInStringArray(srdB.str_valid_headers, keyheader);

            if (keyIndexA == -1 || keyIndexB == -1) return;

            Dictionary<string, List<string>> keyValuePairsA = new Dictionary<string, List<string>>();
            Dictionary<string, List<string>> keyValuePairsB = new Dictionary<string, List<string>>();

            string keyValueA, keyValueB;
            //A 기준 전체 데이터 list에 적재
            for (int r = 1; r < srdA.str_load_values.GetLength(0) - 1; r++)
            {
                //행마다 초기화
                keyValueA = null;
                List<string> valuesA = new List<string>();

                for (int c = 0; c < srdA.str_valid_headers.Length; c++)
                {
                    if (keyIndexA == c)
                    {
                        keyValueA = srdA.str_load_values[r, c];
                    }
                    else //키 값 제외 순서대로 리스트 추가
                    {
                        valuesA.Add(srdA.str_load_values[r, c]);
                    }
                }

                //딕셔너리 완성
                if (!string.IsNullOrEmpty(keyValueA) && !keyValuePairsA.ContainsKey(keyValueA))
                    keyValuePairsA.Add(keyValueA, valuesA);
                else if (!string.IsNullOrEmpty(keyValueA))
                    Log.Information($"[A] Dulplicated Key! {keyValueA}");
            }

            //B는 map 참조해서 A의 헤더 순서대로 데이터 적재가 필요하다
            for (int r = 1; r < srdB.str_load_values.GetLength(0) - 1; r++)
            {
                //행마다 초기화
                keyValueB = null;
                List<string> valuesB = new List<string>();

                for (int c = 0; c < srdB.str_valid_headers.Length; c++)
                {
                    if (keyIndexB == map[c])
                    {
                        keyValueB = srdB.str_load_values[r, map[c]];
                    }
                    else //키 값 제외 순서대로 리스트 추가
                    {
                        valuesB.Add(srdB.str_load_values[r, map[c]]);
                    }
                }

                //딕셔너리 완성
                if (!string.IsNullOrEmpty(keyValueB) && !keyValuePairsB.ContainsKey(keyValueB))
                    keyValuePairsB.Add(keyValueB, valuesB);
                else if (!string.IsNullOrEmpty(keyValueB))
                    Log.Information($"[B] Dulplicated Key! {keyValueB}");
            }

            //차이점 도출
            //A 기준
            write_arr = new string[keyValuePairsA.Keys.Count + 2, (srdA.str_valid_headers.Length * 2) - 1];

            int wr = 0;
            int wc = 0;

            //KeyHeader 중심
            write_arr[wr, wc++] = keyheader; //(0,0)

            //나머지 헤더열 작성
            for (int c = 0; c < srdA.str_valid_headers.Length; c++)
            {
                if (c != keyIndexA)
                {
                    write_arr[wr, wc++] = $"[A] {srdA.str_valid_headers[c]}";
                    write_arr[wr, wc++] = $"[B] {srdA.str_valid_headers[c]}";
                }
            }

            wr = 1;
            foreach (var A in keyValuePairsA)
            {
                wc = 0;

                write_arr[wr, wc++] = A.Key;

                List<string> valuesB = new List<string>();
                //A의 key가 B에 없는 경우
                if (!keyValuePairsB.TryGetValue(A.Key, out valuesB))
                {
                    onlyListA.Add(A.Key);
                }
                else
                {
                    //int nExistDiff = 0;
                    int[] map_arr = new int[srdA.str_valid_headers.Length - 1];
                    for (int i = 0; i < map_arr.Length; i++)
                        map_arr[i] = 0;

                    for (int i = 0; i < srdA.str_valid_headers.Length - 1; i++)
                    {
                        string a = A.Value[i];
                        string b = valuesB[i];

                        if (string.IsNullOrEmpty(a) && !string.IsNullOrEmpty(a))
                        {//a 값이 공백인 경우
                            write_arr[wr, wc++] = "-";
                            write_arr[wr, wc++] = b;

                            map_arr[i]++;
                        }
                        else if (!string.IsNullOrEmpty(a) && string.IsNullOrEmpty(a))
                        {
                            write_arr[wr, wc++] = a;
                            write_arr[wr, wc++] = "-";

                            map_arr[i]++;
                        }
                        else if (string.IsNullOrEmpty(a) && string.IsNullOrEmpty(b))
                        {
                            if (nMode == 1) //all
                            {
                                write_arr[wr, wc++] = "-";
                                write_arr[wr, wc++] = "-";
                            }
                        }
                        else if (!a.Equals(b))
                        {
                            if (nMode == 1)
                            {
                                write_arr[wr, wc++] = a;
                                write_arr[wr, wc++] = b;
                            }
                            else
                            {
                                int idx = (i * 2) + 1;
                                write_arr[wr, idx] = a;
                                write_arr[wr, idx + 1] = b;
                            }

                            map_arr[i]++;
                        }
                        else
                        {
                            if (nMode == 1)
                            {
                                write_arr[wr, wc++] = "-";
                                write_arr[wr, wc++] = "-";
                            }
                        }
                    }

                    if (nMode == 2)
                    {
                        //모두 동일한 지 여부 확인
                        int nTotal = 0;
                        for (int i = 0; i < map_arr.Length; i++)
                            nTotal += map_arr[i];

                        //모두 동일한 행에 대해선 write pointer를 증가시키지 않는다
                        if (nTotal > 0)
                        {
                            wr++;
                        }
                        else
                        {
                            //마지막 key값 쓰는 것 방지
                            write_arr[wr, 0] = "";
                        }
                    }
                    else
                        wr++;
                }
            }

            write_row = wr;

            foreach (var B in keyValuePairsB)
            {
                List<string> values = new List<string>();
                //A의 key가 B에 없는 경우
                if (!keyValuePairsA.TryGetValue(B.Key, out values))
                {
                    onlyListB.Add(B.Key);
                }
            }
        }

        public string GetS5DPostion(string var)
        {
            return GetS5DPositionWithoutMM(var).ToString();
        }

        public double GetFRMPosition(string var)
        {
            double d = -1;
            if (string.IsNullOrEmpty(var))
                return -1;
            else
            {
                if (Double.TryParse(var, out d))
                {
                    return Math.Round(d, 0);
                }
                else
                    return -1;
            }
        }

        public double GetS5DPositionWithoutMM(string str)
        {
            if (string.IsNullOrEmpty(str))
                return -1;
            else
            {
                int subIndex = str.IndexOf("m") - 2;
                if (subIndex >= 0)
                {
                    string var = str.Substring(1, subIndex);
                    return Math.Round(Double.Parse(var), 0);
                }
                else
                    return -1;
            }
        }

        public void GerHeaderListFromSRD(SheetReadData srd, int nHeaderRow, out List<string> list)
        {
            //Log.Information($"GerHeaderListFromSRD {nHeaderRow}");

            list = new List<string>();

            //header
            int nRow = nHeaderRow;
            int nCol = srd.obj_used_load_values.GetLength(1) + 1;

            for (int i = 1; i < nCol; i++)
            {
                string str = SettingObject2String(srd.obj_used_load_values[nRow, i]);
                list.Add(str);
            }            
        }
        
        public int GerHeaderRowFromSRD(SheetReadData srd)
        {
            int nRow = srd.obj_used_load_values.GetLength(0) + 1;
            int nCol = srd.obj_used_load_values.GetLength(1) + 1;

            for (int r = 1; r < nRow-2; r++)
            {
                for (int c = 1; c < nCol-2; c++)
                {
                    if (null != srd.obj_used_load_values[r, c] &&
                        null != srd.obj_used_load_values[r, c + 1] &&
                        null != srd.obj_used_load_values[r, c + 2])
                        return r;
                }
            }

            return -1;
        }

        public bool ReadMultiFilesFromSelectedPathWithSheetNoAndHeader(string folderpath,
            int nTargetSheet,
            out List<SheetReadData> list)
        {
            list = new List<SheetReadData>();

            return true;
        }

        
        public string GetUTinLNFromLateralNumber(string ln)
        {
            if (string.IsNullOrEmpty(ln))
                return ln;

            string[] sp = ln.Split('_');
            if (sp.Length < 3)
                return ln;

            return sp[1];
        }

        public string GetPjtHeader(string lineName)
        {
            if (IsStringContains(lineName, "PH1"))//if (dropDownItemName.Equals("X2-PH1"))
                return "X21";
            else if (IsStringContains(lineName, "PH2"))//if (dropDownItemName.Equals("X2-PH2"))
                return "X22";

            return "X1";
        }

        public string GetUTFromConnectedUtility(string connectedUT)
        {
            if (string.IsNullOrEmpty(connectedUT))
                return string.Empty;

            string ut = connectedUT;

            int nStartIndexOf = connectedUT.IndexOf('(');
            int nEndIndexOf = connectedUT.IndexOf(')');

            int cnt = CountCharInString(connectedUT, "(");

            // #1. 단순 (*) 구조 --Connector_#2_13 (PN2)
            if (cnt == 1)
            {
                ut = connectedUT.Substring(nStartIndexOf + 1, nEndIndexOf - nStartIndexOf - 1);
            }
            else if (cnt == 2)
            {
                // #2. 중첩 (()) 구조 --25A (IWW2(VAT))
                if (connectedUT.LastIndexOf("))") > 0)
                {
                    nEndIndexOf = connectedUT.LastIndexOf(')');
                    ut = connectedUT.Substring(nStartIndexOf + 1, nEndIndexOf - nStartIndexOf - 1);
                }
                // #3. 병렬 ()(*) 구조 --Connector name(Gas box)(exhaust) 
                else
                {
                    nStartIndexOf = connectedUT.LastIndexOf('(');
                    nEndIndexOf = connectedUT.LastIndexOf(')');

                    ut = connectedUT.Substring(nStartIndexOf + 1, nEndIndexOf - nStartIndexOf - 1);
                }
            }
                        // #4. 복잡 ()()()()(*) --B28_3/4" DHF(1:200)(O3HF) RETURN_PFA(3rd) (Gas box)(exhaust) 
            // #5. 복잡 ()()()((*)) --B28_3/4" DHF(1:200)(O3HF) RETURN_PFA(3rd) (200HF(DHF))
            // #6. PREFIX(UT) (UT) --50A_FORE-LINE(LL) (FORE-LINE(LL))
            // #7. PREFIX(UT(중첩)) --TOF(CABINET(전용))
            else if (cnt > 2)
            {
                // 패턴 1: "PREFIX(UT) (UT)" 형태 - 마지막 " (" 이후의 내용을 추출
                int lastSpaceOpenParen = connectedUT.LastIndexOf(" (");
                if (lastSpaceOpenParen > 0)
                {
                    nStartIndexOf = lastSpaceOpenParen;
                    nEndIndexOf = connectedUT.LastIndexOf(')');
                    ut = connectedUT.Substring(nStartIndexOf + 1, nEndIndexOf - nStartIndexOf - 1);
                }
                // 패턴 2: "PREFIX(UT(중첩))" 형태 - 마지막 "(" 이후의 내용을 추출
                else
                {
                    nStartIndexOf = connectedUT.LastIndexOf('(');
                    nEndIndexOf = connectedUT.LastIndexOf(')');
                    ut = connectedUT.Substring(nStartIndexOf + 1, nEndIndexOf - nStartIndexOf - 1);
                }
            }

            //Log.Information("[HELPER] GetUTFromConnectedUtility " + connectedUT + " cnt: " + cnt.ToString() + " => " + ut);

            return ut;
        }

        public int GetValidHeaderRow(SheetReadData data)
        {
            int nMaxRow = 100;
            int nMaxCol = 100;

            if (data.obj_used_load_values.GetLength(0) < nMaxRow)
                nMaxRow = data.obj_used_load_values.GetLength(0);
            if (data.obj_used_load_values.GetLength(1) < nMaxCol)
                nMaxCol = data.obj_used_load_values.GetLength(1);

            //유효한 헤더를 찾는 로직 추가
            for (int r = 1; r < nMaxRow; r++)
            {
                for (int c = 1; c < nMaxCol; c++)
                {
                    if (data.obj_used_load_values[r, c] == null) continue;

                    string str = SettingObject2String(data.obj_used_load_values[r, c]);

                    //Log.Information($"[{r},{c}] {str}");

                    if (!string.IsNullOrEmpty(str) &&
                        data.str_valid_headers.Contains(str))
                    {
                        //Log.Information($"*** Find Header Row {r}:{str}");
                        return r;
                    }                   
                }
            }

            return -1;
        }

        public void CastObject2StringForHookUpPlus(SheetReadData data)
        {
            int ReadRow = 1;
            int ReadColumn = 1;
            int nSheetDataStartRow = GetSheetDataStartRow(data);
            int nMaxRow = data.obj_used_load_values.GetLength(0);
            int nMaxCol = data.obj_used_load_values.GetLength(1);
            List<int> validColIndexList = data.validColumIndex.ValidIndexList;
            data.str_load_values = new string[nMaxRow, nMaxCol];

            for (int i = nSheetDataStartRow; i < nMaxRow; i++)
            {
                if (data.nSheetIndex != 1 && data.nSheetIndex != 4 && data.obj_used_load_values[i, 2] == null) continue;
                if (data.nSheetIndex == 4 && data.obj_used_load_values[i, 1] == null) continue;

                for (int j = validColIndexList.Min(); j <= validColIndexList.Max(); j++)
                {
                    if (!data.validColumIndex.IsValidIndex(j)) continue;

                    Log.Information($"[CastObject2StringForHookUpPlus] {ReadRow}{ReadColumn} <-- OBJ[{i},{j}]:{SettingObject2String(data.obj_used_load_values[i, j])}");

                    data.str_load_values[ReadRow, ReadColumn++] = SettingObject2String(data.obj_used_load_values[i, j]);                    
                }

                ReadRow++;
                ReadColumn = 1;
            }

            data.nValidMaxRow = ReadRow;

            /*
            string strDebug = string.Empty;
            for (int i = 1; i < 10; i++)
            {
                for (int j = 1; j < 5; j++)
                {
                    strDebug += data.str_load_values[i, j] + " ";
                }
                Log.Information(strDebug);
                strDebug = "";
            }
            */
        }

        public void CastObject2StringForHookUp(SheetReadData data)
        {
            int ReadRow = 1;
            int ReadColumn = 1;
            int nSheetDataStartRow = GetSheetDataStartRow(data);
            int nMaxRow = data.obj_used_load_values.GetLength(0) + 1;
            int nMaxCol = data.obj_used_load_values.GetLength(1) + 1;
            List<int> validColIndexList = data.validColumIndex.ValidIndexList;
            data.str_load_values = new string[nMaxRow, nMaxCol];

            Log.Information($"CastObject2StringForHookUp: {data.strSheetName} " +
                $"nSheetDataStartRow:{nSheetDataStartRow} nMaxRow:{nMaxRow} nMaxCol:{nMaxCol} validColIndexList:{validColIndexList.Count}");

            for (int OpenExcelRow = nSheetDataStartRow; OpenExcelRow < nMaxRow; OpenExcelRow++)
            {
                if (data.nSheetIndex != 1 && data.nSheetIndex != 4 && data.obj_used_load_values[OpenExcelRow, 2] == null) continue;
                if (data.nSheetIndex == 4 && data.obj_used_load_values[OpenExcelRow, 1] == null) continue;

                for (int OpenExcelcol = validColIndexList.Min(); OpenExcelcol <= validColIndexList.Max(); OpenExcelcol++)
                {
                    if (!data.validColumIndex.IsValidIndex(OpenExcelcol)) continue;

                    data.str_load_values[ReadRow, ReadColumn++] = SettingObject2String(data.obj_used_load_values[OpenExcelRow, OpenExcelcol]);
                }

                ReadRow++;
                ReadColumn = 1;
            }

            data.nValidMaxRow = ReadRow;

            /*
            string strDebug = string.Empty;
            for (int i = 1; i < 10; i++)
            {
                for (int j = 1; j < 5; j++)
                {
                    strDebug += data.str_load_values[i, j] + " ";
                }
                Log.Information(strDebug);
                strDebug = "";
            }
            */
        }

        public bool FindValidColumnIndex(SheetReadData data, int nMaxCol = int.MaxValue)
        {
            if (null == data)
            {
                Log.Information($"FindValidColumnIndex ERROR! SRD NULL {data}");
                return false;
            }

            if (data.str_valid_headers == null || data.str_valid_headers.Length == 0)
            {
                Log.Information($"FindValidColumnIndex ERROR! str_valid_headers error {data.strSheetName}");
                return false;
            }

            if (null == data.obj_used_load_values || 0 >= data.obj_used_load_values.GetLength(0) || 0 >= data.obj_used_load_values.GetLength(1))
            {
                Log.Information($"FindValidColumnIndex ERROR! obj_used_load_values error");
                return false;
            }

            Log.Information($"** Valid Input Header Len:{data.str_valid_headers.Length}" +
                $" Read Raw Header Len:{data.obj_used_load_values.GetLength(1)}");
            
            data.nSheetHeaderRow = GetValidHeaderRow(data);
            Log.Information($"data.nSheetHeaderRow: {data.nSheetHeaderRow}");
            if (data.nSheetHeaderRow == -1)
            {
                Log.Information($"FindValidColumnIndex ERROR! GetValidHeaderRow error {data.nSheetHeaderRow}");
                return false;
            }

            int nColMaxVal = data.obj_used_load_values.GetLength(1);
            if (nColMaxVal > nMaxCol)
                nColMaxVal = nMaxCol;

            data.validColumIndex = new ValidColumIndex(data.str_valid_headers.Length + 1);
            data.str_load_values = new string[data.obj_used_load_values.GetLength(0) + 1, data.obj_used_load_values.GetLength(1) + 1];

            int nWriteCol = 0;
            for (int i = 1; i <= nColMaxVal; i++)
            {
                string header = SettingObject2String(data.obj_used_load_values[data.nSheetHeaderRow, i]);
                if (!string.IsNullOrEmpty(header))
                {
                    if (data.str_valid_headers.Contains(header) /*||
                        data.str_valid_headers.Where(x => header.Contains(x)).Count() > 0*/)
                    {
                        data.validColumIndex.ValidIndexList.Add(i);
                        data.str_load_values[0, nWriteCol] = header;
                        Log.Information($"\t[{nWriteCol}] {header.Trim()} Excel:{i} SRD:{nWriteCol}");
                        nWriteCol++;
                    }
                }
                string header2 = SettingObject2String(data.obj_used_load_values[data.nSheetHeaderRow+1, i]);
                if (!string.IsNullOrEmpty(header2))
                {
                    if (data.str_valid_headers.Contains(header2) /*||
                        data.str_valid_headers.Where(x => header.Contains(x)).Count() > 0*/)
                    {
                        data.validColumIndex.ValidIndexList.Add(i);
                        data.str_load_values[0, nWriteCol] = header2;
                        Log.Information($"\t[{nWriteCol}] {header2.Trim()} Excel:{i} SRD:{nWriteCol}");
                        nWriteCol++;
                    }
                }
            }

            return true;
        }

        public bool SetValidSheetHeaders(SheetReadData srd, string[] strarr)
        {
            if (null == srd)
            {
                Log.Information("Set ValidSheetHeaders srd NULL");
                return false;
            }

            if (null == strarr)
            {
                Log.Information("Set ValidSheetHeaders header NULL");
                return false;
            }

            srd.str_valid_headers = strarr;

            return true;
        }

        public int GetSheetDataStartRow(SheetReadData data)
        {
            if (-1 != data.nSheetHeaderRow)
                return data.nSheetHeaderRow + 1;

            return -1;
        }

        public bool IsValidValue(string var)
        {
            if (string.IsNullOrEmpty(var) || var.Equals("-"))
                return false;

            return true;
        }

        public bool IsInvalidValue(string var)
        {
            if (string.IsNullOrEmpty(var) || var.Equals("-"))
                return true;

            return false;
        }

        public string GetDateValueByHeaderName(SheetReadData srd, int r, string header_name)
        {
            return GetShortDateStringFromDateString(GetValueByHeaderName(srd, r, header_name));
        }

        public string GetValueByHeaderName(SheetReadData srd, int r, string header_name)
        {
            if (null == srd || null == srd.str_valid_headers || 0 > r)
                return "-";

            //Log.Information($"[DEBUG] GetValueByHeaderName r:{r} header name:{header_name}");

            int index = -1;
            for (int i = 0; i < srd.str_valid_headers.Length; i++)
            {
                //if (header_name.Equals(srd.str_valid_headers.ElementAt(i)))
                if (IsStringContains(srd.str_valid_headers[i], header_name))
                {
                    index = i;
                    //Log.Information($"[DEBUG] GetValueByHeaderName Find! " +
                    //    $"index:{index} " +
                    //    $"value:{srd.str_load_values[r, index]}");
                    break;
                }
            }

            if (0 > index)
            {
                Log.Information($"[DEBUG] GetValueByHeaderName Can't Find Matched r:{r} header name:{header_name}");
                return "-";
            }

            //index 값이 불러온 값을 초과하는 경우
            if (index >= srd.str_load_values.GetLength(1))
            {
                Log.Information($"[DEBUG] GetValueByHeaderName Over Index Error! index:{index} > colmax:{srd.str_load_values.GetLength(1)} header name:{header_name}");
                return "-";
            }

            if (string.IsNullOrEmpty(srd.str_load_values[r, index]))
                return "-";

            return srd.str_load_values[r, index];
        }
    }

    public class JLicenceUser
    {
        public string MACAddress;
        public string UserName;
        public string AuthorizedGroup;
        public string AccessApps;

        public JLicenceUser()
        { }

        public JLicenceUser(string mACAddress, string userName, string authorizedGroup, string accessApps)
        {
            MACAddress = mACAddress;
            UserName = userName;
            AuthorizedGroup = authorizedGroup;
            AccessApps = accessApps;
        }
    }

    public class ValidColumIndex
    {
        public List<int> ValidIndexList;
        public int nEssentialCol;
        public ValidColumIndex(int nSize)
        {
            this.ValidIndexList = new List<int>(nSize);
            this.nEssentialCol = 0;
        }
        public int GetEssentialCol()
        {
            return this.nEssentialCol;
        }

        public bool IsValidIndex(int index)
        {
            return this.ValidIndexList.Contains(index);
        }
    }

    public class SheetReadData
    {
        public string strSheetName;
        public int nSheetIndex;
        public int nSheetHeaderRow;
        public int nSheetUsedRangeStartRow;
        public int nStartReadRow;
        public string strValidity;
        public object[,] obj_used_load_values;
        public string[,] str_load_values;
        public ValidColumIndex validColumIndex;
        public int nValidMaxRow;
        public string[] str_valid_headers;

        public SheetReadData()
        {
            ;
        }

        public SheetReadData(int nStartReadRow, int nSheetHeaderRow)
        {
            this.nStartReadRow = nStartReadRow;
            this.nSheetHeaderRow = nSheetHeaderRow;
        }
    }

    public class ValidSheetCheck
    {
        public int nSheetIndex;
        public int nRow;
        public int nCol;
        public string strValidValue;

        public ValidSheetCheck(int i, int r, int c, string s)
        {
            this.nSheetIndex = i;
            this.nRow = r;
            this.nCol = c;
            this.strValidValue = s;
        }
    }

    public class ExcelFileInfo
    {
        public string strFileFullPath;
        public string strFilePathWithoutFileName;
        public string strFileName;
        public List<ValidSheetCheck> vsclist;
        public List<SheetReadData> srdlist;

        public ExcelFileInfo()
        {
            this.vsclist = new List<ValidSheetCheck>();
            this.srdlist = new List<SheetReadData>();
        }
    }

    public class CompareData
    {
        public string key;
        public List<string> data;

        public CompareData()
        {
            this.data = new List<string>();
        }
    }
    public enum WRITE_MODE
    {
        FOLLOW_VALID_HEADER_COL = 0,
        FOLLOW_ORG_COL = 1
    }

    public enum HEADER_FIND
    {
        SIMILAR = 0,
        EXACTLY,
    }
}

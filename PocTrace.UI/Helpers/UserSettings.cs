using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using PocTrace.Core.Infrastructure;

namespace PocTrace.UI.Helpers
{
    /// <summary>
    /// 사용자 설정을 저장하고 불러오는 클래스.
    /// 공유 폴더가 아니라 %LocalAppData%\PocTrace\Viewer 에 둔다.
    /// </summary>
    public class UserSettings
    {
        private const string SettingsFileName = "user_settings.json";

        // 설정 속성들
        public string SelectedDb { get; set; } = "";
        public string SearchText { get; set; } = "";
        public string BatchInputText { get; set; } = "";
        public int SelectedPropListIndex { get; set; } = 0;
        public bool EnableLog { get; set; } = true;
        public bool EnableDebug { get; set; } = false;
        public List<string> RecentSearches { get; set; } = new List<string>();
        public int FormWidth { get; set; } = 1200;
        public int FormHeight { get; set; } = 800;
        public int FormLocationX { get; set; } = 100;
        public int FormLocationY { get; set; } = 100;
        public bool FormMaximized { get; set; } = false;

        /// <summary>
        /// 설정을 파일에서 불러옵니다.
        /// </summary>
        public static UserSettings Load()
        {
            try
            {
                var path = GetSettingsFilePath();
                if (File.Exists(path))
                {
                    string json = TextFileEncoding.ReadAllText(path);
                    var settings = JsonConvert.DeserializeObject<UserSettings>(json);
                    return settings ?? new UserSettings();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load settings: {ex.Message}");
            }

            return new UserSettings();
        }

        /// <summary>
        /// 설정을 파일에 저장합니다.
        /// </summary>
        public void Save()
        {
            try
            {
                var settings = new JsonSerializerSettings
                {
                    Formatting = Formatting.Indented,
                    NullValueHandling = NullValueHandling.Ignore
                };

                string json = JsonConvert.SerializeObject(this, settings);
                TextFileEncoding.WriteAllText(GetSettingsFilePath(), json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to save settings: {ex.Message}");
            }
        }

        /// <summary>
        /// 최근 검색어를 추가합니다.
        /// </summary>
        public void AddRecentSearch(string searchText)
        {
            if (string.IsNullOrWhiteSpace(searchText)) return;

            RecentSearches.Remove(searchText);
            RecentSearches.Insert(0, searchText);

            if (RecentSearches.Count > 10)
            {
                RecentSearches.RemoveAt(RecentSearches.Count - 1);
            }
        }

        /// <summary>
        /// 설정 파일 경로를 반환합니다.
        /// </summary>
        public static string GetSettingsFilePath()
        {
            var local = PocTraceLocalPaths.CombineApp(PocTraceLocalPaths.Viewer, SettingsFileName);
            if (File.Exists(local))
                return local;

            var legacy = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, SettingsFileName);
            if (File.Exists(legacy))
            {
                try
                {
                    File.Copy(legacy, local, overwrite: false);
                }
                catch
                {
                    // 복사 실패 시에도 로컬 경로에 새로 저장한다.
                }
            }

            return local;
        }
    }
}

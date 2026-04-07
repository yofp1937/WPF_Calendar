/*
 * 파일 쓰기, 읽기 담당
 * 파일 저장은 '/사용자폴더/AppData/Local/yofp/TodoCalendar' 에 저장할 예정
 */
using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace Calendar.Common.Util
{
    public static class FileHelper
    {
        #region Property
        // Path.Combie - 운영체제에 맞게 경로 설정할때 슬래쉬, 역슬래쉬 자동 처리해주는 함수
        // Environment.GetFolderPath - 폴더 경로 가져오는 함수
        // Environment.SpecialFolder.LocalApplicationData - 윈도우 환경설정에서 "Local AppData"로 지정된 절대 경로를 자동으로 찾아줌
        private static readonly string FolderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "yofp", "TodoCalendar");

        // JsonSerializerOptions는 매번 새로 만들면 성능에 좋지 않으므로 static으로 한 번만 선언
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            // 사람이 보기쉽게 줄바꿈, 들여쓰기 할것인지
            WriteIndented = true,
            // 데이터가 null일 경우 Json 파일에 항목 기록 자체를 하지않음
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };
        #endregion

        #region 메서드
        /// <summary>
        /// 경로에 폴더가 존재하는지 확인 후 없으면 생성
        /// </summary>
        private static void CreateFolderIfNotExist()
        {
            if (!Directory.Exists(FolderPath))
                Directory.CreateDirectory(FolderPath);
        }

        /// <summary>
        /// FolderPath 반환
        /// </summary>
        /// <returns></returns>
        public static string GetFolderPath()
        {
            CreateFolderIfNotExist();
            return FolderPath;
        }

        /// <summary>
        /// 데이터를 Json 형식으로 변환하여 저장
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="fileName">파일 이름</param>
        /// <param name="data">저장할 데이터</param>
        public static void SaveJson<T>(string fileName, T data)
        {
            CreateFolderIfNotExist();

            string filePath = Path.Combine(FolderPath, fileName);
            try
            {
                string jsonString = JsonSerializer.Serialize(data, JsonOptions);

                // using을 사용하여 쓰기가 끝나면 즉시 파일에대한 작업을 종료
                using (FileStream fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
                using (StreamWriter sw = new StreamWriter(fs))
                {
                    sw.Write(jsonString);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[FileHelper]: SaveJson 실패 - {ex.Message}");
            }
        }

        /// <summary>
        /// Json 형식의 데이터 불러오기
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="fileName">파일 이름</param>
        /// <returns></returns>
        public static T? LoadJson<T>(string fileName) where T : class
        {
            string filePath = Path.Combine(FolderPath, fileName);
            if (!File.Exists(filePath))
                return null;

            try
            {
                // FileShare.ReadWrite를 사용해 다른 곳에서 임시 파일을 원본으로 바꾸는 중이라도 안전하게 읽게함
                using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    return JsonSerializer.Deserialize<T>(fs, JsonOptions);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[FileHelper]: LoadJson 실패 - {ex.Message}");
                return null;
            }
        }
        #endregion
    }
}

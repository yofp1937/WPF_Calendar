/*
 * 프로그램의 데이터 전체 관리
 */
using Calendar.Common.Interface;
using Calendar.Common.Util;
using Calendar.Model;
using Calendar.Model.DataClass;
using Calendar.Model.DataClass.TodoEntities;
using Calendar.Model.Enum;
using System.Diagnostics;
using System.IO;

namespace Calendar.Manager
{
    public class DataManager : ITodoRepository, ISettingRepository
    {
        #region Property
        // Json 파일 저장명
        private const string TodoFileName = "TodoData.json";
        private const string SettingFileName = "Settings.json";
        private const string RoutineRecordsFolderName = "RoutineRecords";
        // 파일 동시 접근 방지
        private readonly object _fileLock = new object();

        // 프로그램 실행 중 메모리에 들고 있을 데이터 객체
        private TodoStorage _currentStorage;
        private AppSettings _appSettings;

        // 3초 뒤 저장 기능 구현을 위한 Token
        private CancellationTokenSource? _saveCts;
        #endregion

        #region 생성자
        public DataManager()
        {
            _currentStorage = LoadTodoDataFromFile();
            _appSettings = LoadSettingsFromFile();
            CheckLastUpdated(_currentStorage);
        }
        #endregion

        #region public
        public async Task<bool> AddOrUpdateData_AsyncSave<T>(T data) where T : class
        {
            if (data is BaseTodoData todoData)
            {
                await AddOrUpdateData_AsyncSave(todoData);
                return true;
            }
            return false;
        }

        public async Task<bool> DeleteData_AsyncSave<T>(T data) where T : class
        {
            if (data is BaseTodoData todoData)
            {
                await DeleteData_AsyncSave(todoData);
                return true;
            }
            else if (data is IEnumerable<BaseTodoData> todoDataList)
            {
                await DeleteData_AsyncSave(todoDataList);
                return true;
            }
            return false;
        }

        public TodoStorage GetTodoStorage()
        {
            return _currentStorage;
        }

        public void RequestSaveAfter3Seconds()
        {
            SaveAfter3seconds();
        }

        public void WaitingForSavingData()
        {
            Task.Run(async () =>
            {
                await SaveTodoDataAsync();
                await SaveSettingsDataAsync();
            }).GetAwaiter().GetResult();
        }

        public AppSettings GetSettings()
        {
            return _appSettings;
        }
        public async Task<bool> SaveSettings_AsyncSave(AppSettings settings)
        {
            try
            {
                _appSettings = settings;
                await SaveSettingsDataAsync();
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DataManager]: SaveSettings 실패 - {ex.Message}");
                return false;
            }
        }
        #endregion

        #region private
        #region Data 불러오기, Data 검사 및 변경
        /// <summary>
        /// 파일에서 데이터를 불러와 메모리에 적재
        /// </summary>
        private TodoStorage LoadTodoDataFromFile()
        {
            // 파일이 없으면 새 객체를 생성
            TodoStorage data = FileHelper.LoadJson<TodoStorage>(TodoFileName) ?? new TodoStorage();
            return data;
        }

        private AppSettings LoadSettingsFromFile()
        {
            AppSettings settings = FileHelper.LoadJson<AppSettings>(SettingFileName) ?? new AppSettings();
            return settings;
        }

        /// <summary>
        /// Json 데이터를 불러올때 마지막 업데이트 날짜 체크
        /// </summary>
        private void CheckLastUpdated(TodoStorage data)
        {
            if (DateTime.Today > data.LastUpdated)
            {
                UpdateExpiredTasks(data);
                data.LastUpdated = DateTime.Today.Date; // 업데이트 날짜 갱신

                _ = SaveTodoDataAsync();
            }
        }

        /// <summary>
        /// 날짜가 바뀐 경우, 완료되지 않은 과거 데이터를 실패로 처리
        /// </summary>
        private void UpdateExpiredTasks(TodoStorage data)
        {
            DateTime today = DateTime.Today;

            // 일정 중 오늘 이전의 날짜이면서 완료되지 않은 것 실패 처리
            foreach (ScheduleData schedule in data.Schedules.Where(s => s.StartDate.Date < today && s.Status == TodoStatus.Waiting))
            {
                schedule.Status = TodoStatus.Failure;
            }

            // 규칙 중 오늘 이전의 날짜이면서 완료되지 않은 것 실패 처리
            foreach (RoutineRecord record in data.RoutineRecords.Where(r => r.Date.Date < today && r.Status == TodoStatus.Waiting))
            {
                record.Status = TodoStatus.Failure;
            }
        }
        #endregion
        #region Data 저장
        /// <summary>
        /// 실제 파일에 데이터를 저장하는 비동기 로직
        /// </summary>
        private async Task SaveTodoDataAsync()
        {
            // Task.Run을 사용하여 백그라운드 스레드에서 작업 수행
            await Task.Run(() =>
            {
                lock (_fileLock) // 여러 곳에서 동시에 파일을 쓰는 것을 방지
                {
                    // folderPath를 지정하지않으면 현재 프로그램이 실행되고있는 폴더에서 상대 경로로 파일을 찾게됨
                    // 절대 경로를 지정해서 FileHelper에서 저장한 Json 파일이 있는곳에서 작업을 실행해야함
                    string folderPath = FileHelper.GetFolderPath();
                    string filePath = Path.Combine(folderPath, TodoFileName);
                    string tempPath = Path.Combine(folderPath, TodoFileName + ".tmp");
                    try
                    {
                        // 데이터 저장중 프로그램이 종료되면 파일이 깨질 위험 존재하므로
                        // 임시 파일에 우선 저장
                        FileHelper.SaveJson(tempPath, _currentStorage);

                        // 임시 파일과 원본 파일이 존재하면 원본 삭제
                        if (File.Exists(tempPath) && File.Exists(filePath))
                        {
                            File.Delete(filePath);
                        }

                        // 임시 파일을 원본으로 변경
                        File.Move(tempPath, filePath);
                        //Debug.WriteLine($"[DataManager]: SaveTodoDataAsync - 파일 저장 완료");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[DataManager]: SaveTodoDataAsync - 파일 저장 실패: {ex.Message}");
                    }
                }
            });
        }

        /// <summary>
        /// 실제 파일에 데이터를 저장하는 비동기 로직
        /// </summary>
        private async Task SaveSettingsDataAsync()
        {
            await Task.Run(() =>
            {
                lock (_fileLock)
                {
                    string folderPath = FileHelper.GetFolderPath();
                    string filePath = Path.Combine(folderPath, SettingFileName);
                    string tempPath = Path.Combine(folderPath, SettingFileName + ".tmp");
                    try
                    {
                        // 임시 파일에 저장한 뒤 원본으로 변경
                        FileHelper.SaveJson(tempPath, _appSettings);
                        if (File.Exists(tempPath) && File.Exists(filePath))
                        {
                            File.Delete(filePath);
                        }
                        File.Move(tempPath, filePath);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[DataManager]: SaveSettingsDataAsync - 파일 저장 실패: {ex.Message}");
                    }
                }
            });
        }

        /// <summary>
        /// 3초 뒤에 저장하도록 예약하는 비동기 메서드
        /// </summary>
        private async void SaveAfter3seconds()
        {
            // 이미 대기중인 예약이 있다면 취소 (타이머 초기화)
            _saveCts?.Cancel();
            _saveCts = new CancellationTokenSource();
            try
            {
                // 비동기로 3초 대기 (이 동안 UI는 멈추지 않음)
                await Task.Delay(3000, _saveCts.Token);
                // 33초가 지나면 실제 파일 저장 실행
                await SaveTodoDataAsync();
                //Debug.WriteLine($"[DataManager]: SaveAfter3seconds - 3초 지나서 성공적으로 데이터 저장됨");
            }
            catch (TaskCanceledException)
            {
                // 새로 SaveAfter3seconds가 호출되서 기존 _saveCts가 취소된 경우
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DataManager]: SaveAfter3seconds - 오류 발생\n{ex.Message}");
            }
        }

        /// <summary>
        /// 데이터를 형식에따라 저장소에 넣고 Json으로 저장
        /// </summary>
        private async Task AddOrUpdateData_AsyncSave(BaseTodoData data)
        {
            // 원본 객체가 존재하면 삭제
            DeleteDataInCurrentStorage(data);

            // 데이터 추가
            if (data is ScheduleData s) _currentStorage.Schedules.Add(s);
            else if (data is RoutineData r) _currentStorage.Routines.Add(r);
            else if (data is RoutineRecord rr) _currentStorage.RoutineRecords.Add(rr);
            await SaveTodoDataAsync();
        }
        #endregion
        #region Data 제거
        /// <summary>
        /// 전달받은 데이터의 데이터 형식에 따라 저장소에서 데이터 삭제
        /// </summary>
        private bool DeleteDataInCurrentStorage(BaseTodoData data)
        {
            var original = _currentStorage.FindOriginal(data);
            if (original == null) return false;

            return original switch
            {
                ScheduleData s => _currentStorage.Schedules.Remove(s),
                RoutineData r => RemoveRoutineWithGarbageRecords(r),
                RoutineRecord rr => _currentStorage.RoutineRecords.Remove(rr),
                _ => false
            };
        }

        /// <summary>
        /// 저장소에서 RoutineData 제거하고 RoutineRecords까지 정리
        /// </summary>
        private bool RemoveRoutineWithGarbageRecords(RoutineData routine)
        {
            _currentStorage.ClearGarbageRecords(routine);
            return _currentStorage.Routines.Remove(routine);
        }

        /// <summary>
        /// 데이터를 제거하고 저장
        /// </summary>
        private async Task DeleteData_AsyncSave(BaseTodoData data)
        {
            if(DeleteDataInCurrentStorage(data))
            {
                await SaveTodoDataAsync();
            }
        }

        /// <summary>
        /// 데이터들을 전부 제거하고 한번만 저장
        /// </summary>
        private async Task DeleteData_AsyncSave(IEnumerable<BaseTodoData> datas)
        {
            bool isDeleted = false;
            foreach (var data in datas.ToList()) // 리스트 복사해서 순회
            {
                if (DeleteDataInCurrentStorage(data))
                    isDeleted = true;
            }

            if (isDeleted)
                await SaveTodoDataAsync();
        }
        #endregion
        /// <summary>
        /// 임시로 만들어둔 메서드임 수정해야함
        /// 넘겨받은 existingData는 어제부로 종료시키고 newData는 저장소에 저장한다.<br/>
        /// 하지만 newData의 입력된 값중 EndDate가 오늘 이전이면 return한다.(생성 취소)<para/>
        /// true - 기존 데이터 제거하고 새로운 루틴 생성 성공<br/>
        /// false - EndDate가 오늘 이전이라 데이터 제거와 루틴 생성 모두 실패
        /// </summary>
        /// <param name="setFailure">true: Status를 Failure로 변경, false: Status를 Waiting으로 설정</param>
        public bool TempEditRoutineAndRegister(RoutineData existingData, RoutineData newData)
        {
            // newData의 EndDate가 오늘 날짜 이전이면 차단(false 반환)
            if (!newData.IsIndefinite && newData.EndDate < DateTime.Today)
            {
                return false;
            }
            // 기존 데이터 처리
            _currentStorage.FinishedOrRemoveRoutineData(existingData);
            // 새로운 데이터 처리
            _currentStorage.AddRoutineWithPastRecords(newData, false);
            return true;
        }
        public bool TempAddNewRoutine(RoutineData routineData)
        {
            try
            {
                _currentStorage.AddRoutineWithPastRecords(routineData, true);
                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }
        #endregion
    }
}

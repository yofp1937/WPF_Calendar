/*
 * 프로그램의 데이터 전체 관리
 */
using Calendar.Common.Interface;
using Calendar.Common.Messages;
using Calendar.Common.Util;
using Calendar.Model;
using Calendar.Model.DataClass;
using Calendar.Model.DataClass.TodoEntities;
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
        // 파일 동시 접근 방지
        private readonly object _fileLock = new object();

        // 프로그램 실행 중 메모리에 들고 있을 데이터 객체
        private readonly ITodoStorage _currentStorage;
        private AppSettings _appSettings;

        // 3초 뒤 저장 기능 구현을 위한 Token
        private CancellationTokenSource? _saveCts;
        #endregion

        #region 생성자
        public DataManager()
        {
            _currentStorage = LoadTodoDataFromFile();
            _appSettings = LoadSettingsFromFile();
            CallCheckLastUpdatedInTodoStorage();
            SubscribeMessenger();
        }
        #endregion

        #region Method
        #region public Method
        #region ITodoRepository 구현
        /// <inheritdoc cref="ITodoRepository.GetTodoStorage"/>
        public ITodoStorage GetTodoStorage()
        {
            return _currentStorage;
        }
        /// <inheritdoc cref="ITodoRepository.AddOrUpdateData_AsyncSave{T}(T, bool)"/>
        public async Task<bool> AddOrUpdateData_AsyncSave<T>(T data, bool isNewRoutineData = false) where T : BaseTodoData
        {
            try
            {
                if (_currentStorage.AddOrUpdateData(data, isNewRoutineData))
                {
                    await SaveTodoDataAsync();
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DataManager - AddOrUpdateData_AsyncSave]: 오류 발생 {ex}");
                return false;
            }
        }
        /// <inheritdoc cref="ITodoRepository.RemoveData_AsyncSave{T}(T)"/>
        public async Task<bool> RemoveData_AsyncSave<T>(T data) where T : BaseTodoData
        {
            try
            {
                if(_currentStorage.RemoveData(data))
                {
                    await SaveTodoDataAsync();
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DataManager - DeleteData_AsyncSave]: 오류 발생 {ex}");
                return false;
            }
        }
        /// <inheritdoc cref="ITodoRepository.RemoveData_AsyncSave{T}(IEnumerable{T})"/>
        /// <remarks>datas의 내용물을 하나씩 꺼내 ITodoStroage에 RemoveData를 요청합니다.</remarks>
        public async Task<bool> RemoveData_AsyncSave<T>(IEnumerable<T> datas) where T : BaseTodoData
        {
            try
            {
                bool isComplete = false;
                // datas에 들어있는 data를 하나씩 삭제 요청
                foreach (var data in datas)
                {
                    // 하나라도 삭제에 성공하면 isComplete = true로 변경
                    if (_currentStorage.RemoveData(data))
                        isComplete = true;
                }
                // 하나라도 삭제됐으면 저장
                if (isComplete)
                {
                    await SaveTodoDataAsync();
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DataManager - DeleteData_AsyncSave]: 오류 발생 {ex}");
                return false;
            }
        }
        /// <inheritdoc/>
        /// <remarks>ITodoStorage에서 <see cref="TodoStorage.CloseOrRemoveRoutineData(RoutineData)"/>를 사용해 existingData는 자동으로 종료시키거나 삭제합니다.</remarks>
        public async Task<bool> UpdateRoutineData_AsyncSave(RoutineData existingData, RoutineData newData)
        {
            try
            {
                // 기존 데이터는 제거 요청
                bool isCompleted = false;
                isCompleted = _currentStorage.RemoveData(existingData);
                // 신규 데이터는 추가 요청
                isCompleted = _currentStorage.AddOrUpdateData(newData);
                if (isCompleted)
                {
                    await SaveTodoDataAsync();
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DataManager - UpdateRoutineData_AsyncSave]: 오류 발생 {ex}");
                return false;
            }
        }
        /// <inheritdoc/>
        public void WaitingForSavingData()
        {
            Task.Run(async () =>
            {
                await SaveTodoDataAsync();
                await SaveSettingsDataAsync();
            }).GetAwaiter().GetResult();
        }
        #endregion
        #region ISettingRepository 구현
        /// <inheritdoc/>
        public AppSettings GetSettings()
        {
            return _appSettings;
        }
        /// <inheritdoc/>
        /// Todo: 나중엔 AppSettings를 변경하는게 아닌 내부 설정값을 변경하게끔 수정해야함
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
        #endregion
        #region private
        /// <summary>
        /// Local 저장소에서 TodoStorage 데이터를 전부 읽어온 이후 CheckLastUpdated 메서드를 호출시킵니다.
        /// </summary>
        private void CallCheckLastUpdatedInTodoStorage()
        {
            _currentStorage.CheckLastUpdated();
        }
        /// <summary>
        /// DataManager의 생성자에서 구독해야할 Message들을 정의합니다.
        /// </summary>
        private void SubscribeMessenger()
        {
            Messenger.Subscribe<DataMessages.SaveDataAfter3Seconds>(this, _ =>
            {
                // TODO: SaveDataAfter3Seconds 만들기만하고 작동을 안하고있었음 DataManger, TodoStorage 전부 수정하고 테스트 해볼것
                Debug.WriteLine($"SaveDataAfter3Seconds 메세지 확인");
                SaveAfter3seconds();
            });
        }
        #region Data 불러오기
        /// <summary>
        /// Local 저장소에서 일정, 규칙에 관련된 Json 파일을 데이터로 읽어와 메모리에 적재합니다.
        /// </summary>
        private TodoStorage LoadTodoDataFromFile()
        {
            // 파일이 없으면 새 객체를 생성
            TodoStorage data = FileHelper.LoadJson<TodoStorage>(TodoFileName) ?? new TodoStorage();
            return data;
        }

        /// <summary>
        /// Local 저장소에서 프로그램 설정에 관련된 Json 파일을 데이터로 읽어와 메모리에 적재합니다.
        /// </summary>
        private AppSettings LoadSettingsFromFile()
        {
            // 파일이 없으면 새 객체를 생성
            AppSettings settings = FileHelper.LoadJson<AppSettings>(SettingFileName) ?? new AppSettings();
            return settings;
        }
        #endregion
        #region Data Json 저장
        /// <summary>
        /// ITodoStorage를 Local 저장소에 Json으로 저장하는 비동기 로직
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
                        // 데이터 저장중 프로그램이 종료되면 파일이 깨질 위험 존재하므로 임시 파일에 우선 저장
                        // _currentStorage를 저장하면 Interface 기반으로 저장돼서 형변환하여 저장해야함
                        if(_currentStorage is TodoStorage storage)
                            FileHelper.SaveJson(tempPath, storage);

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
        /// AppSettings를 Local 저장소에 Json으로 저장하는 비동기 로직
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
        /// ITodoStorage를 Local 저장소에 3초 뒤에 Json으로 저장하도록 예약하는 비동기 메서드
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
        #endregion
        #endregion
        #endregion
    }
}

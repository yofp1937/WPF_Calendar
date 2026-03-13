/*
 * 일정, 규칙 수정을 위한 TodoViewModel
 */
using Calendar.Common.Interface;
using Calendar.Common.Messages;
using Calendar.Common.Util;
using Calendar.Model.DataClass.TodoEntities;
using Calendar.Model.Enum;
using System.Diagnostics;

namespace Calendar.ViewModel.TodoWindow
{
    public class EditTodoViewModel : TodoBaseViewModel
    {
        #region Property
        private ScheduleData? _scheduleData;
        private RoutineData? _routineData;
        private RoutineRecord? _routineRecord;
        #endregion

        #region 생성자, override
        // 생성될때 RoutineData, ScheduleData를 받아와서 화면에 표시해줘야함
        public EditTodoViewModel(ITodoRepository todoRepository, BaseTodoData todoData) : base(todoRepository)
        {
            IsAddMode = false;
            if (todoData is ScheduleData scheduleData)
            {
                IsRoutine = false;
                MainBarTitleText = "일정 수정";
                _scheduleData = scheduleData;
            }
            else
            {
                IsRoutine = true;
                MainBarTitleText = "규칙 수정";
                switch (todoData)
                {
                    case RoutineInstance routineInstance:
                        _routineData = routineInstance.ParentRoutineData;
                        _routineRecord = routineInstance;
                        break;
                    case RoutineData routineData:
                        _routineData = routineData;
                        break;
                    case RoutineRecord routineRecord:
                        _routineRecord = routineRecord;
                        break;
                }
            }
            SetData();
        }

        protected override void SubmitExecute(object? obj)
        {
            // 1. 필수 데이터가 모두 입력됐는지 확인
            if (!CheckRequiredData())
                return;

            if (IsRoutine)
            {
                OperateRoutineEditProcess();
            }
            else if (_scheduleData != null)
            {
                ApplyCommonAndStatusData(_scheduleData);
                _ = TodoRepository.AddOrUpdateData_AsyncSave(_scheduleData);
            }
            Messenger.Send(new TodoMessages.RefreshTodoUI());
            CloseWindow();
        }
        protected override void DeleteExecute(object? obj)
        {
            DateTime today = DateTime.Today;
            if (IsRoutine)
            {
                if (_routineRecord == null) return;

                // 1. 과거 데이터면 RoutineRecord만 제거
                if (today > _routineRecord.Date.Date || _routineData == null)
                {
                    _ = TodoRepository.DeleteData_AsyncSave(_routineRecord);
                }
                // 2. 오늘, 미래 규칙이면 RoutineData 제거
                if (_routineData != null && _routineRecord.Date.Date >= today)
                {
                    _ = TodoRepository.DeleteData_AsyncSave(_routineData);
                }
            }
            else
            {
                if (_scheduleData == null) return;
                _ = TodoRepository.DeleteData_AsyncSave(_scheduleData);
            }
            Messenger.Send(new TodoMessages.RefreshTodoUI());
            CloseWindow();
        }
        #endregion

        #region 메서드
        /// <summary>
        /// _schduleData, _routineData를 기반으로 EditWindow의 UI 값 세팅
        /// </summary>
        private void SetData()
        {
            // Schedule일 경우 일정 수정 Data Setting
            if (!IsRoutine && _scheduleData != null)
            {
                SetUICommonAndStatusDataFromTarget(_scheduleData);
            }
            // Routine일 경우 규칙 수정 Data Setting
            else if (IsRoutine)
            {
                // RoutineRecord가 있으면 Record 우선으로 채움
                if (_routineRecord != null)
                {
                    SetUICommonAndStatusDataFromTarget(_routineRecord);
                    // RoutineData가 존재하면 RoutineData 값도 추가로 보완
                    if (_routineData != null)
                    {
                        SelectedRoutineType = _routineData.RoutineType;
                        IsIndefinite = _routineData.IsIndefinite;
                        EndDate = IsIndefinite ? _routineData.StartDate.AddYears(1) : _routineData.EndDate;
                        SelectedComboBoxItem = _routineData.Frequency - 1;
                    }
                    else
                    {
                        IsIndefinite = false;
                        EndDate = _routineRecord.Date;
                    }
                }
                // RoutineData 수정일 경우
                else if (_routineData != null)
                {
                    SetUICommonDataFromTarget(_routineData);
                    SelectedRoutineType = _routineData.RoutineType;
                    IsIndefinite = _routineData.IsIndefinite;
                    EndDate = IsIndefinite ? _routineData.StartDate.AddYears(1) : _routineData.EndDate;
                    SelectedComboBoxItem = _routineData.Frequency - 1;
                }
                // RoutineRecord 수정일 경우

                if (CurrentRoutineVM is IRoutineViewModel routineVM)
                {
                    if (_routineData == null) return;
                    // SelectedRoutineType에 맞춰 data 세팅
                    object? data = SelectedRoutineType switch
                    {
                        RoutineType.Weekly => _routineData.SelectedWeeklyDays,
                        RoutineType.Monthly => _routineData.SelectedMonthlyDates,
                        RoutineType.Yearly => _routineData.SelectedYearlyDates,
                        _ => null
                    };

                    if (data != null)
                        routineVM.SetRoutineData(data);
                }
            }
        }

        /// <summary>
        /// RoutineData의 TodoTitle, TodoContent를 제외한 핵심 변수들이 변경됐는지 확인<br/>
        /// true: 하나라도 변함, false: 하나도 안변함
        /// </summary>
        private bool CheckRoutineDataModified()
        {
            if (_routineData == null) return false;

            if (_routineData.StartDate.Date != StartDate.Date) return true;
            if (_routineData.RoutineType != SelectedRoutineType) return true;
            if (_routineData.Frequency != SelectedComboBoxItem + 1) return true;
            // 기한 없음 체크 해제돼있고 EndDate 다르면
            if (_routineData.IsIndefinite != IsIndefinite) return true;
            if (!IsIndefinite && _routineData.EndDate != EndDate) return true;

            if (CurrentRoutineVM is IRoutineViewModel routineVM)
            {
                object tempData = routineVM.GetRoutineData();

                return tempData switch
                {
                    List<DayOfWeek> weekly => !AreListsEqual(_routineData.SelectedWeeklyDays, weekly),
                    List<int> monthly => !AreListsEqual(_routineData.SelectedMonthlyDates, monthly),
                    List<DateTime> yearly => !AreListsEqual(_routineData.SelectedYearlyDates, yearly),
                    _ => false
                };
            }
            Debug.WriteLine($"CheckRoutineDataModified: 핵심 변수 안변함 false");
            return false;
        }

        /// <summary>
        /// 두 리스트의 내용물이 동일한지 비교하는 메서드
        /// </summary>
        private bool AreListsEqual<T>(List<T>? list1, List<T>? list2)
        {
            // 1. 둘 다 null이면 같은 것으로 간주
            if (list1 == null && list2 == null) return true;

            // 2. 어느 한쪽만 null이면 다른 것으로 간주
            if (list1 == null || list2 == null) return false;

            // 3. 개수가 다르면 무조건 다름
            if (list1.Count != list2.Count) return false;

            // 4. 내용물 비교 (순서와 상관없이 구성 요소가 같은지 확인)
            return list1.All(item => list2.Contains(item)) && list2.All(item => list1.Contains(item));
        }

        /// <summary>
        /// 어느 Routine에 접근했냐에따라 처리과정이 다른데 처리과정을 정확하게 실행해주는 함수
        /// </summary>
        private void OperateRoutineEditProcess()
        {
            // 루틴 아니면 retrun
            if (!IsRoutine || _routineRecord == null) return;

            DateTime today = DateTime.Today;

            // 미래의 데이터를 수정하는가?
            if (_routineRecord.Date > today)
            {
                UpdateFutureRoutine();
            }
            else
            {
                _routineRecord.Status = CurrentStatus;
                // 1. 과거의 RoutineRecord을 수정하려하는가?
                if (today > _routineRecord.Date)
                {
                    UpdatePastRoutine();
                }
                // 2. 오늘 날짜의 Routine을 수정하려하는가?
                else if (today == _routineRecord.Date)
                {
                    UpdateTodayRoutine();
                }
            }
        }

        /// <summary>
        /// 미래의 RoutineRecord를 수정할때 호출
        /// </summary>
        private void UpdateFutureRoutine()
        {
            if (_routineData == null) return;

            // RoutineData의 핵심 데이터가 바뀌었으면
            if(CheckRoutineDataModified())
            {
                // 기존 루틴은 어제부로 종료하고
                TodoRepository.GetTodoStorage().FinishedOrRemoveRoutineData(_routineData);

                // 새로운 루틴 생성하여 추가
                RoutineData newData = ApplyRoutineData(new());
                if(TodoRepository.TempEditRoutineAndRegister(_routineData, newData))
                    _ = TodoRepository.AddOrUpdateData_AsyncSave(newData);
            }
            // Title, Content만 수정됐으면
            else
            {
                // 기존 루틴의 Title, Content만 수정하고 저장
                _routineData.TodoTitle = TitleTextBox.Trim();
                _routineData.TodoContent = ContentTextBox?.Trim() ?? string.Empty;
                _ = TodoRepository.AddOrUpdateData_AsyncSave(_routineData);
            }
        }

        /// <summary>
        /// 과거의 RoutineRecord를 수정할때 호출<br/>
        /// RoutineRecord를 수정할땐 StartDate를 수정할 수 없음 
        /// </summary>
        private void UpdatePastRoutine()
        {
            if (_routineRecord == null) return;
            _routineRecord.TodoTitle = TitleTextBox.Trim();
            _routineRecord.TodoContent = ContentTextBox?.Trim() ?? string.Empty;
            _routineRecord.Status = CurrentStatus;

            _ = TodoRepository.AddOrUpdateData_AsyncSave(_routineRecord);
        }

        /// <summary>
        /// 오늘 날짜의 RoutineRecord를 수정할때 호출
        /// </summary>
        private void UpdateTodayRoutine()
        {
            if (_routineData == null || _routineRecord == null) return;

            // 1. Title, Content만 수정했을 경우 RoutineRecord를 수정
            if (!CheckRoutineDataModified())
            {
                // 1-1. RoutineRecord에 UI에서 입력받은 데이터를 넣고 수정해야함
                ApplyCommonAndStatusData(_routineRecord);
                _ = TodoRepository.AddOrUpdateData_AsyncSave(_routineRecord);
            }
            // 2. 그외 주요 데이터를 수정했을 경우 RoutineData를 수정
            else
            {
                RoutineData newData = ApplyRoutineData(new());
                if(TodoRepository.TempEditRoutineAndRegister(_routineData, newData))
                    _ = TodoRepository.AddOrUpdateData_AsyncSave(newData);
            }
        }
        #endregion
    }
}

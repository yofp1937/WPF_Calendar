/*
 * 일정 목록 Window의 ViewModel
 * 더블클릭시 편집창 열리게 만들기
 */
using Calendar.Common.Commands;
using Calendar.Common.Interface;
using Calendar.Common.Messages;
using Calendar.Common.Util;
using Calendar.Model.DataClass;
using Calendar.Model.DataClass.TodoEntities;
using Calendar.Model.Enum;
using Calendar.ViewModel.Base;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows.Input;

namespace Calendar.ViewModel.ListWindow
{
    public class ListWindowViewModel : WindowBaseViewModel
    {
        #region Property
        // 선택된 데이터 타입(일정, 규칙, 과거 규칙)
        private TodoListType _listType = TodoListType.ScheduleDataType;
        public TodoListType ListType
        {
            get => _listType;
            set
            {
                if (SetProperty(ref _listType, value))
                {
                    IsAllChecked = false;
                    ClearAllCheckBox();
                }
            }
        }

        // 현재 선택된 Item(수정 버튼에 넘어갈 데이터)
        private BaseTodoData? _selectedItem;
        public BaseTodoData? SelectedItem
        {
            get => _selectedItem;
            set => SetProperty(ref _selectedItem, value);
        }

        // 전체 체크 버튼과 Binding
        private bool _isAllChecked;
        public bool IsAllChecked
        {
            get => _isAllChecked;
            set
            {
                if (SetProperty(ref _isAllChecked, value))
                {
                    ToggleAllItems(value);
                }
            }
        }

        // 각각의 데이터 표시를 위한 ObservableCollection
        public ObservableCollection<ScheduleData> ScheduleDataList { get; set; } = new();
        public ObservableCollection<RoutineData> RoutineDataList { get; set; } = new();
        public ObservableCollection<RoutineRecord> RoutineRecordList { get; set; } = new();

        // 체크박스 체크된 Data List
        public ObservableCollection<BaseTodoData> CheckedList { get; set; } = new();

        // 삭제 버튼 연결
        public ICommand? DeleteCheckedItemCommand { get; private set; }
        // ListView Item 더블클릭 이벤트
        public ICommand? EditBtnAndDoubleClickCommand { get; private set; }
        #endregion

        #region 생성자, override
        public ListWindowViewModel(ITodoRepository todoRepository) : base(todoRepository, null)
        {
            MainBarTitleText = "일정 목록";
            LoadDataList();

            // 메신저 구독(RefreshTodoUI 라는 신호가 들어오면 LoadDataList 실행 예약)
            Messenger.Subscribe<TodoMessages.RefreshTodoUI>(this, _ => LoadDataList());
        }

        protected override void RegisterICommands()
        {
            base.RegisterICommands();

            DeleteCheckedItemCommand = new RelayCommand(DeleteCheckedItemExecute);
            EditBtnAndDoubleClickCommand = new RelayCommand(DoubleClickExecute);
        }
        #endregion

        #region 메서드
        /// <summary>
        /// ListType이 변경될때 Checkbox들의 check 상태 초기화
        /// </summary>
        private void ClearAllCheckBox()
        {
            // IsChecked가 변하면 OnItemPropertyChanged에 의해 CheckList의 값이 계속 변하는데
            // foreach가 돌아가는중에 List가 변경되면 오류가 발생하기때문에
            // ToList로 복사본 생성하여 진행
            foreach (BaseTodoData data in CheckedList.ToList())
            {
                data.IsChecked = false;
            }
            CheckedList.Clear();
        }

        /// <summary>
        /// 생성자에서 호출하여 초기화 후 DataList에 타입에 맞는 데이터를 집어넣음
        /// </summary>
        private void LoadDataList()
        {
            TodoStorage storage = TodoRepository.GetTodoStorage();
            ScheduleDataList.Clear();
            RoutineDataList.Clear();
            RoutineRecordList.Clear();

            // 데이터를 List에 채우기 전 각 데이터들에 OnItemPropertyChanged이벤트 등록
            // 1. ScheduleData 채우기
            foreach (ScheduleData schedule in storage.Schedules)
            {
                ConnectEventToData(schedule);
                ScheduleDataList.Add(schedule);
            }
            // 2. RoutineData 채우기
            foreach (RoutineData routineData in storage.Routines)
            {
                ConnectEventToData(routineData);
                RoutineDataList.Add(routineData);
            }
            // 3. RoutineRecord 채우기
            // 정렬은 날짜 -> 생성 순서 순으로 오름차순 정렬
            var sortedRecords = storage.RoutineRecords.OrderBy(r => r.Date).ThenBy(r => r.CreatedTicks);
            foreach (RoutineRecord routineRecord in sortedRecords)
            {
                ConnectEventToData(routineRecord);
                RoutineRecordList.Add(routineRecord);
            }
        }

        /// <summary>
        /// 데이터에 이벤트 부여
        /// </summary>
        private void ConnectEventToData(BaseTodoData data)
        {
            // 기존 연결 이벤트를 제거하여 중복연결 방지하고, 이벤트 연결
            data.PropertyChanged -= OnItemPropertyChanged;
            data.PropertyChanged += OnItemPropertyChanged;
        }

        /// <summary>
        /// CheckBox Check 신호 들어오면 검사해서 CheckedList에 추가, 제거
        /// </summary>
        private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // 입력된 이벤트가 BaseTodoData의 IsChecked가 아니면 return
            if (e.PropertyName != nameof(BaseTodoData.IsChecked)) return;

            // 각 데이터들을 검사
            if (sender is BaseTodoData data)
            {
                // IsChecked가 true면
                if (data.IsChecked)
                {
                    // CheckedList에 포함됐는지 검사하고 없으면 추가
                    if (!CheckedList.Contains(data))
                        CheckedList.Add(data);
                }
                // IsChecked가 false면
                else
                {
                    // CheckedList에서 데이터 제거
                    CheckedList.Remove(data);
                }
            }
        }

        /// <summary>
        /// CheckBox로 체크해둔것들 전부 삭제
        /// </summary>
        private async void DeleteCheckedItemExecute(object? obj)
        {
            if (CheckedList.Count == 0) return;

            // 전부 지워질때까지 대기
            await TodoRepository.DeleteData_AsyncSave(CheckedList.ToList());

            ClearAllCheckBox();
            LoadDataList();
            Messenger.Send(new TodoMessages.RefreshTodoUI());
        }

        /// <summary>
        /// 모든 체크박스 체크, 체크해제
        /// </summary>
        private void ToggleAllItems(bool isChecked)
        {
            // 현재 보고 있는 탭의 리스트만 골라서 전체 처리
            switch (ListType)
            {
                case TodoListType.ScheduleDataType:
                    foreach (ScheduleData item in ScheduleDataList)
                        item.IsChecked = isChecked;
                    break;
                case TodoListType.RoutineDataType:
                    foreach (RoutineData item in RoutineDataList)
                        item.IsChecked = isChecked;
                    break;
                case TodoListType.RoutineRecordType:
                    foreach (RoutineRecord item in RoutineRecordList)
                        item.IsChecked = isChecked;
                    break;
            }
        }

        private void DoubleClickExecute(object? obj)
        {
            if (obj == null) return;
            Messenger.Send(new WindowMessages.OpenWindowMessage(obj));
        }
        #endregion
    }
}

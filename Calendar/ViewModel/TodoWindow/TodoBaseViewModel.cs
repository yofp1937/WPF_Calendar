/*
 * AddTodoViewModel, EditTodoViewModel의 기반 ViewModel 클래스
 */
using Calendar.Common.Commands;
using Calendar.Common.Interface;
using Calendar.Model.DataClass.TodoEntities;
using Calendar.Model.Enum;
using Calendar.ViewModel.Base;
using Calendar.ViewModel.TodoWindow.Routine;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Input;

namespace Calendar.ViewModel.TodoWindow
{
    public abstract class TodoBaseViewModel : WindowBaseViewModel
    {
        #region Property
        // TodoWindow 자체에 필요한 변수
        // 데이터 추가인지, 데이터 수정인지 여부
        public bool IsAddMode { get; protected set; }
        // 일정인지, 규칙인지
        public bool IsRoutine { get; protected set; }

        // 경고 텍스트
        private string _warningText = string.Empty;
        public string WarningText
        {
            get => _warningText;
            set => SetProperty(ref _warningText, value);
        }

        // 필수 데이터 입력했는지 경고 Text 표시 여부
        private bool _isWarningVisible = false;
        public bool IsWarningVisible
        {
            get => _isWarningVisible;
            set => SetProperty(ref _isWarningVisible, value);
        }

        #region Schedule, Routine 공동 사용 변수
        // 제목 TextBox
        private string _titleTextBox = string.Empty;
        public string TitleTextBox
        {
            get => _titleTextBox;
            set
            {
                if (SetProperty(ref _titleTextBox, value) && IsWarningVisible && !string.IsNullOrWhiteSpace(value))
                    IsWarningVisible = false;
            }
        }
        // 시작 날짜 DatePicker
        private DateTime _startDate = DateTime.Today;
        public DateTime StartDate
        {
            get => _startDate;
            set => SetProperty(ref _startDate, value);
        }
        // 상세 내용 TextBox
        private string? _contentTextBox;
        public string? ContentTextBox
        {
            get => _contentTextBox;
            set => SetProperty(ref _contentTextBox, value);
        }

        // 라디오 버튼 바인딩용
        private TodoStatus _currentStatus = TodoStatus.Waiting;
        public TodoStatus CurrentStatus
        {
            get => _currentStatus;
            set => SetProperty(ref _currentStatus, value);
        }
        #endregion
        #region Routine에 필요한 변수
        // 일간 ~ 연간 어떤 타입의 규칙 추가인지
        private RoutineType _selectedRoutineType;
        public RoutineType SelectedRoutineType
        {
            get => _selectedRoutineType;
            set
            {
                if (SetProperty(ref _selectedRoutineType, value))
                {
                    UpdateComboBoxItem();
                    UpdateRoutineViewModel();
                }
            }
        }
        // SelectedRotuineType에 따른 ViewModel
        private BaseViewModel? _currentRoutineVM;
        public BaseViewModel? CurrentRoutineVM
        {
            get => _currentRoutineVM;
            set => SetProperty(ref _currentRoutineVM, value);
        }
        // 종료 날짜 존재하는지 true = 미존재, false = 존재
        private bool _isIndefinite = true;
        public bool IsIndefinite
        {
            get => _isIndefinite;
            set => SetProperty(ref _isIndefinite, value);
        }
        // 종료 날짜
        private DateTime? _endDate = DateTime.Now.AddYears(1);
        public DateTime? EndDate
        {
            get => _endDate;
            set => SetProperty(ref _endDate, value);
        }
        // 빈도 ComboBox (ComboBoxItems에서 몇번째 Item이 선택됐는지 체크)
        private int _selectedComboBoxItem;
        public int SelectedComboBoxItem
        {
            get => _selectedComboBoxItem;
            set => SetProperty(ref _selectedComboBoxItem, value);
        }
        // UI 체크박스 바인딩용 Property (SelectedComboBoxItem와 연결)
        public ObservableCollection<string> ComboBoxItems { get; } = new ObservableCollection<string>();
        // 과거 RoutineRecord 수정할때 Title, Content를 제외한 다른 Element 잠궈버리는 용도
        private bool _isPastDateReadOnly;
        public bool IsPastDateReadOnly
        {
            get => IsPastDateReadOnly;
            set => SetProperty(ref _isPastDateReadOnly, value);
        }
        #endregion

        public ICommand? SubmitCommand { get; protected set; }
        public ICommand? DeleteCommand { get; protected set; }
        #endregion

        #region 생성자, override
        protected TodoBaseViewModel(ITodoRepository todoRepository) : base(todoRepository, null) { }
        protected override void RegisterICommands()
        {
            base.RegisterICommands();
            SubmitCommand = new RelayCommand(SubmitExecute);
            DeleteCommand = new RelayCommand(DeleteExecute);
        }
        #endregion

        #region abstract, virtual
        /// <summary>
        /// SubmitCommand와 연결된 메서드(자식들이 구현해야함)
        /// </summary>
        protected abstract void SubmitExecute(object? obj);
        /// <summary>
        /// DeleteCommand와 연결된 메서드(사용하는곳에서 구현)
        /// </summary>
        protected virtual void DeleteExecute(object? obj) { }
        /// <summary>
        /// 필수 데이터(제목, Routine의 경우엔 오른쪽 반복 날짜 선택까지)가 입력됐는지 검사
        /// </summary>
        protected virtual bool CheckRequiredData()
        {
            // 1. 제목이 입력됐는가
            if (string.IsNullOrWhiteSpace(TitleTextBox))
            {
                Debug.WriteLine("제목입력x");
                WarningText = "제목을 입력하세요.";
                IsWarningVisible = true;
                return false;
            }

            // 2. 규칙일 경우 추가 검증
            if (IsRoutine && CurrentRoutineVM is IRoutineViewModel routineVM)
            {
                if (!routineVM.GetEnteredRequireData())
                {
                    WarningText = "반복 날짜 선택을 완료하세요.";
                    IsWarningVisible = true;
                    return false;
                }
            }
            return true;
        }
        #endregion

        #region 메서드
        /// <summary>
        /// Target의 Data를 추출하여 UI에 바인딩 된 값에 옮겨줌
        /// </summary>
        protected void SetUICommonDataFromTarget<T>(T target) where T : BaseTodoData
        {
            TitleTextBox = target.TodoTitle;
            StartDate = target.StartDate;
            ContentTextBox = target.TodoContent;
        }

        /// <summary>
        /// Target의 Data를 추출하여 UI에 바인딩 된 값에 옮겨줌
        /// </summary>
        protected void SetUICommonAndStatusDataFromTarget<T>(T target) where T : BaseTodoDataWithStatus
        {
            SetUICommonDataFromTarget(target);
            CurrentStatus = target.Status;
        }

        /// <summary>
        /// 전달받은 target 객체에 UI의 공통 데이터를 주입하고 반환
        /// </summary>
        protected T ApplyCommonData<T>(T target) where T : BaseTodoData
        {
            target.TodoTitle = TitleTextBox.Trim();
            target.TodoContent = ContentTextBox?.Trim() ?? string.Empty;
            target.StartDate = StartDate;
            return target;
        }

        /// <summary>
        /// 전달받은 target 객체에 UI의 공통 데이터와 Status를 주입하고 반환
        /// </summary>
        protected T ApplyCommonAndStatusData<T>(T target) where T : BaseTodoDataWithStatus
        {
            ApplyCommonData(target);
            target.Status = CurrentStatus;
            return target;
        }

        /// <summary>
        /// UI 입력값을 기반으로 RoutineData 객체를 생성하고 상세 규칙(주/월/연)을 주입하여 반환
        /// </summary>
        protected RoutineData ApplyRoutineData(RoutineData routineData)
        {
            ApplyCommonData(routineData);
            DateTime? oldEndDate = routineData.EndDate;
            DateTime? newEndDate = IsIndefinite ? null : EndDate;
            routineData.RoutineType = SelectedRoutineType;
            routineData.Frequency = SelectedComboBoxItem + 1;
            routineData.IsIndefinite = IsIndefinite;
            routineData.EndDate = IsIndefinite ? null : EndDate;

            if (CurrentRoutineVM is IRoutineViewModel routineVM)
            {
                object tempData = routineVM.GetRoutineData();
                // tempData의 데이터 형식에따라 주간, 월간, 연간 구분
                switch (tempData)
                {
                    case List<DayOfWeek> weekly:
                        routineData.SelectedWeeklyDays = weekly;
                        //Debug.WriteLine($"주간 규칙: {weekly.Count}");
                        break;
                    case List<int> monthly:
                        routineData.SelectedMonthlyDates = monthly;
                        //Debug.WriteLine($"월간 규칙: {monthly.Count}");
                        break;
                    case List<DateTime> yearly:
                        routineData.SelectedYearlyDates = yearly;
                        //Debug.WriteLine($"연간 규칙: {yearly.Count}");
                        break;
                    default:
                        break;
                }
            }
            return routineData;
        }
        /// <summary>
        /// 주기에 맞게 ComboBox의 접미사 업데이트 (추가하려면 반복문의 i 최대값만 바꾸면됨)
        /// </summary>
        private void UpdateComboBoxItem()
        {
            ComboBoxItems.Clear();

            // 현재 주기에 맞는 단위 설정
            string unit = SelectedRoutineType switch
            {
                RoutineType.Daily => "일",
                RoutineType.Weekly => "주",
                RoutineType.Monthly => "개월",
                RoutineType.Yearly => "년",
                _ => string.Empty
            };
            if (string.IsNullOrEmpty(unit)) return;

            // 반복문으로 리스트 생성
            for (int i = 1; i <= 10; i++)
            {
                if (i == 1)
                {
                    string specialName = SelectedRoutineType switch
                    {
                        RoutineType.Daily => "매일",
                        RoutineType.Weekly => "매주",
                        RoutineType.Monthly => "매월",
                        RoutineType.Yearly => "매년",
                        _ => ""
                    };
                    ComboBoxItems.Add(specialName);
                }
                else
                {
                    ComboBoxItems.Add($"{i}{unit}마다");
                }
            }
            SelectedComboBoxItem = 0;
        }

        /// <summary>
        /// 선택된 주기에따라 ViewModel 생성(View와 Context 매칭은 AddRoutineView.xaml의 ContentControl에서 진행)
        /// </summary>
        private void UpdateRoutineViewModel()
        {
            CurrentRoutineVM = SelectedRoutineType switch
            {
                RoutineType.Daily => new DailyRoutineViewModel(),
                RoutineType.Weekly => new WeeklyRoutineViewModel(),
                RoutineType.Monthly => new MonthlyRoutineViewModel(),
                RoutineType.Yearly => new YearlyRoutineViewModel(),
                _ => null
            };
            //Debug.WriteLine($"{CurrentRoutineVM}");
        }
        #endregion
    }
}

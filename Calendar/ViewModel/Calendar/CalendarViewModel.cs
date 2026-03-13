/*
 * 메인 UI에 달력을 자동으로 생성해주는 클래스
 */
using Calendar.Common.Commands;
using Calendar.Common.Interface;
using Calendar.Common.Messages;
using Calendar.Common.Util;
using Calendar.Model;
using Calendar.Model.DataClass;
using Calendar.Model.DataClass.TodoEntities;
using Calendar.ViewModel.Base;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace Calendar.ViewModel.Calendar
{
    public class CalendarViewModel : BaseViewModel
    {
        private readonly ITodoRepository _todoRepository;
        #region Property
        public ObservableCollection<string> WeekDays { get; private set; } = new ObservableCollection<string>
        {
             "Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat"
        };
        public ObservableCollection<CalendarDayModel> Days { get; private set; } = new();

        private DateTime _currentMonth;
        public DateTime CurrentMonth
        {
            get => _currentMonth;
            set
            {
                int beforeYear = _currentMonth.Year;
                // value가 기존 _currentMonth와 다르면 if문 실행
                if (SetProperty(ref _currentMonth, value))
                {
                    // 연도가 바뀌면 해당 연도의 공휴일을 생성
                    if (beforeYear != _currentMonth.Year)
                        HolidayProvider.InitTargetYaerHolidays(_currentMonth.Year);
                    OnPropertyChanged(nameof(CurrentMonthText));
                    CreateCalendar(CurrentMonth);
                }
            }
        }
        public string CurrentMonthText => $"{_currentMonth.Year}년 {_currentMonth.Month}월";

        private CalendarDayModel? _selectedDay;
        public CalendarDayModel? SelectedDay
        {
            get => _selectedDay;
            set => SetProperty(ref _selectedDay, value);
        }

        public ICommand? PreviousMonthCommand { get; private set; }
        public ICommand? CalendarChangeCommand { get; private set; }
        public ICommand? NextMonthCommand { get; private set; }
        public ICommand? SelectDayCommand { get; private set; }
        #endregion

        #region 생성자, override
        /// <summary>
        /// CalendarBuilder 생성자
        /// </summary>
        public CalendarViewModel(ITodoRepository todoRepository)
        {
            _todoRepository = todoRepository;
            // CurrentMonth 설정으로 달력(Days) 동적 생성
            CurrentMonth = DateTime.Today;
            // Days 생성 이후 SelectedDays를 오늘 날짜로 설정
            SelectedDay = Days.FirstOrDefault(d => d.Date.Date == DateTime.Today.Date);
            if (SelectedDay != null) SelectedDay.IsSelected = true;
            RegisterICommands();

            // 메신저 구독(RefreshTodoUI 라는 신호가 들어오면 LoadSchedules...Calendar 실행 예약)
            Messenger.Subscribe<TodoMessages.RefreshTodoUI>(this, _ => LoadSchedulesAndRoutinesForCurrentCalendar());
        }

        protected override void RegisterICommands()
        {
            PreviousMonthCommand = new RelayCommand(_ => CurrentMonth = CurrentMonth.AddMonths(-1));
            CalendarChangeCommand = new RelayCommand(_ => { /* 달력 클릭 처리 */ });
            NextMonthCommand = new RelayCommand(_ => CurrentMonth = CurrentMonth.AddMonths(1));
            SelectDayCommand = new RelayCommand(SelectDayExecute);
        }
        #endregion

        #region 달력 생성
        /// <summary>
        /// targetMonth에 해당하는 달의 달력 생성
        /// </summary>
        /// <param name="targetMonth">생성을 원하는 달</param>
        public void CreateCalendar(DateTime targetMonth)
        {
            Days.Clear();

            DateTime firstDay = new DateTime(targetMonth.Year, targetMonth.Month, 1);
            int startOffset = (int)firstDay.DayOfWeek; // 0: 일요일 ~ 6: 토요일
            int daysInMonth = DateTime.DaysInMonth(targetMonth.Year, targetMonth.Month); // 이번달이 며칠인지

            // 이전달 공백
            AddPreviousMonthDays(targetMonth.AddMonths(-1), startOffset);

            // 이번달 날짜
            for (int day = 1; day <= daysInMonth; day++)
            {
                DateTime date = new DateTime(CurrentMonth.Year, CurrentMonth.Month, day);
                Days.Add(new CalendarDayModel(date, true));
            }

            // 남은 칸은 다음달 공백
            int totalCells = (Days.Count + 6) / 7 * 7;
            int remainingCells = totalCells - Days.Count;
            AddNextMonthDays(targetMonth.AddMonths(1), remainingCells);

            // 날짜들 일정, 규칙 있는지 확인 후 TextBlock 삽입
            LoadSchedulesAndRoutinesForCurrentCalendar();
        }

        /// <summary>
        /// 현재 표시되는 달력에서 이전달에 해당하는 부분의 날짜를 채워주는 함수
        /// </summary>
        /// <param name="targetDate">이전달이 몇년 몇월인지 매개변수로 넣어야함</param>
        /// <param name="offset">이전달 날짜를 몇개 표시해야하는지 카운트</param>
        private void AddPreviousMonthDays(DateTime targetDate, int offset)
        {
            if (offset == 0) return;

            int daysInMonth = DateTime.DaysInMonth(targetDate.Year, targetDate.Month);
            // daysInMonth = 30, offset이 2일 경우 29, 30의 숫자가 필요한데 +1이 없으면 28, 29가 들어감
            int startDay = daysInMonth + 1 - offset;

            for (int day = startDay; day <= daysInMonth; day++)
            {
                DateTime date = new DateTime(targetDate.Year, targetDate.Month, day); // 날짜 객체 생성
                Days.Add(new CalendarDayModel(date, false));
            }
        }

        /// <summary>
        /// 현재 표시되는 달력에서 다음달에 해당하는 부분의 날짜를 채워주는 함수
        /// </summary>
        /// <param name="targetDate">다음달이 몇년 몇월인지 매개변수로 넣어야함</param>
        /// <param name="offset">다음달 날짜를 몇개 표시해야하는지 카운트</param>
        private void AddNextMonthDays(DateTime targetDate, int offset)
        {
            if (offset == 0) return;

            int daysInMonth = DateTime.DaysInMonth(targetDate.Year, targetDate.Month);

            for (int day = 1; day <= offset; day++)
            {
                DateTime date = new DateTime(targetDate.Year, targetDate.Month, day); // 날짜 객체 생성
                Days.Add(new CalendarDayModel(date, false));
            }
        }

        private void LoadSchedulesAndRoutinesForCurrentCalendar()
        {
            TodoStorage storage = _todoRepository.GetTodoStorage();

            foreach (CalendarDayModel day in Days) // 달력의 칸을 하나씩 검사
            {
                day.DataClear();

                // 1. 일정(Schedule) 검사 (Where은 LINQ 명령어라 List가 아닌 IEnumerable<>로 반환됨)
                IEnumerable<ScheduleData> todaySchedules = storage.Schedules.Where(s => s.StartDate.Date == day.Date.Date);
                foreach (ScheduleData schedule in todaySchedules)
                {
                    day.Schedules.Add(schedule);
                }

                // 2. 과거 규칙(RoutineRecord) 검사
                IEnumerable<RoutineRecord> todayRecords = storage.RoutineRecords.Where(r => r.Date.Date == day.Date.Date);
                // 중복 방지용
                HashSet<Guid> guidHash = new();
                foreach (RoutineRecord record in todayRecords)
                {
                    RoutineData? parentRoutine = storage.Routines.FirstOrDefault(r => r.Id == record.ParentRoutineId);
                    day.RoutineInstances.Add(new RoutineInstance(parentRoutine, record));
                    guidHash.Add(record.ParentRoutineId);
                }

                // 3. 규칙(RoutineData) 검사
                foreach (RoutineData routine in storage.Routines)
                {
                    // 이미 RoutineRecord 검사에서 표시한건 건너뛰기
                    if (guidHash.Contains(routine.Id)) continue;
                    // 종료 날짜가 오늘 이전인 RoutineData는 그리지 않음
                    if (!routine.IsIndefinite && routine.EndDate < DateTime.Today) continue;

                    // 1.오늘 표시돼야하는 Data인지
                    if (routine.IsCheckInDay(day.Date))
                    {
                        // 2. 이 날짜에 저장된 RoutineRecord가 존재하는지
                        RoutineRecord? record = storage.RoutineRecords.FirstOrDefault(r => r.ParentRoutineId == routine.Id && r.Date == day.Date.Date);
                        // RoutineRecords에 Data가 존재하지 않으면 새로 생성
                        if (record == null)
                        {
                            record = new RoutineRecord(routine, day.Date);
                            // 오늘 이전 날짜라면 저장소에 Record 저장
                            if (day.Date.Date <= DateTime.Today)
                            {
                                _ = _todoRepository.AddOrUpdateData_AsyncSave(record);
                            }
                        }
                        day.RoutineInstances.Add(new RoutineInstance(routine, record));
                    }
                }
                day.RefreshView();
            }
        }

        private void SelectDayExecute(object? obj)
        {
            if (obj is CalendarDayModel day)
            {
                // 기존 선택됐던 날짜의 IsSelected 변경
                if (SelectedDay != null)
                    SelectedDay.IsSelected = false;

                SelectedDay = day;
                SelectedDay.IsSelected = true;
            }
        }
        #endregion
    }
}

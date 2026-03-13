/*
 * 화면에 표시될 Data를 담당
 */
using Calendar.Common.Commands;
using Calendar.Common.Interface;
using Calendar.Common.Messages;
using Calendar.Common.Service;
using Calendar.Common.Util;
using Calendar.Model.DataClass;
using Calendar.Model.Enum;
using Calendar.ViewModel.Base;
using Calendar.ViewModel.Calendar;
using Calendar.ViewModel.ListWindow;
using Calendar.ViewModel.TodoWindow;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace Calendar.ViewModel
{
    public class MainViewModel : WindowBaseViewModel
    {
        #region Property
        public CalendarViewModel CalendarVM { get; private set; }
        // SidePanel에서 BInding할 Property
        public CalendarDayModel? CurrentSelectedDay => CalendarVM.SelectedDay;

        private double _windowOpacity = 100.0;
        public double WindowOpacity
        {
            get => _windowOpacity;
            set
            {
                if (SetProperty(ref _windowOpacity, value))
                {
                    SendOpacityChangedMessage(value);
                }
            }
        }

        // MainTitleBar에 존재하는 버튼들은 MainViewModel에서 관리
        public ICommand? OpenAddTasksMenuCommand { get; private set; }
        public ICommand? AddScheduleCommand { get; private set; }
        public ICommand? AddRoutineCommand { get; private set; }
        public ICommand? OpenEditCommand { get; private set; }
        public ICommand? OpenListWindowCommand { get; private set; }
        public ICommand? OpenSettingWindowCommand { get; private set; }
        public ICommand? RestoreWindowCommand { get; private set; }
        public ICommand? ExitProgramCommand { get; private set; }
        #endregion

        #region 생성자, override
        public MainViewModel(ITodoRepository todoRepository, ISettingRepository settingRepository) : base(todoRepository, settingRepository)
        {
            // UI 동적 생성
            CalendarVM = new CalendarViewModel(TodoRepository);
            // SelectedDay가 바뀔때 SidePanel의 UI도 갱신되도록 이벤트 연결
            CalendarVM.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(CalendarVM.SelectedDay))
                {
                    OnPropertyChanged(nameof(CurrentSelectedDay));
                }
            };
        }

        protected override void RegisterICommands()
        {
            // 부모인 WindowBaseVIewModel의 생성자에서 RegisterICommands를 호출했기때문에 여기선 호출 안해도 됨
            base.RegisterICommands();

            OpenAddTasksMenuCommand = new RelayCommand(OpenAddTasksMenuExecute);
            OpenEditCommand = new RelayCommand(OpenEditWindowExecute);
            AddScheduleCommand = new RelayCommand(OpenAddScheduleWindowExecute);
            AddRoutineCommand = new RelayCommand(OpenAddRoutineWindowExecute);
            OpenListWindowCommand = new RelayCommand(OpenListWindowExecute);
            OpenSettingWindowCommand = new RelayCommand(OpenSettingWindowExecute);
            RestoreWindowCommand = new RelayCommand(RestoreWindowExecute);
            ExitProgramCommand = new RelayCommand(ExitProgramExecute);
        }

        protected override void MinimizeWindowExecute(object? obj)
        {
            MinimizeMode currentMode = SettingRepository.GetSettings().MinimizeMode;
            WindowService.Instance.Minimize(this, currentMode);
        }
        #endregion

        #region ICommand 연동 메서드
        private void OpenAddTasksMenuExecute(object? obj)
        {
            // Debug.WriteLine("OpenAddTasksMenuWindowExecute");
            if (obj is System.Windows.Controls.Button btn)
            {
                ContextMenu contextMenu = btn.ContextMenu;
                if (contextMenu != null)
                {
                    // ContextMenu가 버튼 아래에 위치되게 설정
                    contextMenu.PlacementTarget = btn;
                    contextMenu.Placement = PlacementMode.Bottom;
                    btn.ContextMenu.IsOpen = true;
                }
            }
        }
        private void OpenAddScheduleWindowExecute(object? obj)
        {
            AddTodoViewModel todoVM = new AddTodoViewModel(TodoRepository, false);
            Messenger.Send(new WindowMessages.OpenWindowMessage(todoVM));
        }
        private void OpenAddRoutineWindowExecute(object? obj)
        {
            AddTodoViewModel todoVM = new AddTodoViewModel(TodoRepository, true);
            Messenger.Send(new WindowMessages.OpenWindowMessage(todoVM));
        }
        private void OpenEditWindowExecute(object? obj)
        {
            if (obj == null) return;
            Messenger.Send(new WindowMessages.OpenWindowMessage(obj));
        }
        private void OpenListWindowExecute(object? obj)
        {
            ListWindowViewModel listVM = new(TodoRepository);
            Messenger.Send(new WindowMessages.OpenWindowMessage(listVM));
        }
        private void OpenSettingWindowExecute(object? obj)
        {
            SettingViewModel settingVM = new SettingViewModel(SettingRepository);
            Messenger.Send(new WindowMessages.OpenWindowMessage(settingVM));
        }
        #endregion
        #region 메서드
        private void SendOpacityChangedMessage(object? obj)
        {
            if (obj is double opacityValue)
            {
                Messenger.Send(new WindowMessages.UpdateOpacityMessage(opacityValue / 100.0));
            }
        }
        private void TaskbarIcon_TrayMouseDoubleClick(object sender, RoutedEventArgs e)
        {
            WindowService.Instance.RestoreMainWindow();
        }
        private void RestoreWindowExecute(object? obj)
        {
            WindowService.Instance.RestoreMainWindow();
        }
        private void ExitProgramExecute(object? obj)
        {
            WindowService.Instance.ShutDown();
        }
        #endregion
    }
}

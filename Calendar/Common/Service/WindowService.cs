/*
 * Messenger를 통해 ViewModel과 함께 윈도우를 만들어달라는 신호가 넘어오면
 * ShowDialog 메서드를 통해 ViewModel에 맞는 Window를 만들어주기위한 클래스
 */
using Calendar.Common.Interface;
using Calendar.Common.Util;
using Calendar.Model.DataClass.TodoEntities;
using Calendar.Model.Enum;
using Calendar.View;
using Calendar.View.ListWindow;
using Calendar.View.TodoWindow;
using Calendar.ViewModel;
using Calendar.ViewModel.Base;
using Calendar.ViewModel.ListWindow;
using Calendar.ViewModel.TodoWindow;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using Point = System.Windows.Point;

namespace Calendar.Common.Service
{
    public class WindowService : IUIService
    {
        #region Property, 생성자
        private static readonly Lazy<WindowService> _instance = new(() => new WindowService());
        public static WindowService Instance => _instance.Value;

        // 중복 실행 방지
        private bool _isProcessing = false;
        private readonly object _lock = new object();
        private WindowService() { }
        #endregion

        private void ReleaseProcessing()
        {
            lock (_lock) _isProcessing = false;
        }

        #region Window 창 설정
        /// <inheritdoc/>
        public Window? GetWindowByViewModel(object viewModel)
        {
            foreach (Window window in Application.Current.Windows)
            {
                if (window.DataContext == viewModel) return window;
            }
            return null;
        }
        /// <inheritdoc/>
        public void Minimize(object viewModel, MinimizeMode mode = MinimizeMode.Taskbar)
        {
            // lock에 접근 가능하면 Processing 진행이 가능하니 true로 변경 후 메서드 실행
            lock (_lock)
            {
                if(_isProcessing) return;
                _isProcessing = true;
            }

            // window 창을 받아오지 못하면 무시
            Window? window = GetWindowByViewModel(viewModel);
            if (window == null || !window.IsLoaded)
            {
                ReleaseProcessing();
                return;
            }
            window.WindowState = WindowState.Minimized;

            if (mode == MinimizeMode.SystemTray && window is MainWindow)
            {
                window.Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(() =>
                {
                    window.ShowInTaskbar = false;
                    window.Hide();
                    ReleaseProcessing();
                }));
            }
            else
            {
                ReleaseProcessing();
            }
        }
        /// <inheritdoc/>
        public void Maximize(object viewModel)
        {
            Window? window = GetWindowByViewModel(viewModel);
            if (window == null) return;

            if (window.WindowState == WindowState.Maximized)
            {
                window.WindowState = WindowState.Normal;
            }
            else
            {
                SetWindowMaxSize(window);
                window.WindowState = WindowState.Maximized;
            }
        }
        /// <summary>
        /// Window 창의 크기를 작업표시줄을 제외한 실제 가용 영역만큼으로 키움
        /// </summary>
        private void SetWindowMaxSize(Window window)
        {
            // 현재 창이 위치한 모니터를 찾기위해 OS가 관리하는 현재 창의 Handle값을 받아옴
            var handle = new WindowInteropHelper(window).Handle;
            // Handle값을 기반으로 현재 어느 모니터와 가까운지 찾음
            var monitor = Win32Api.MonitorFromWindow(handle, (uint)Win32Api.MonitorOptions.MONITOR_DEFAULTTONEAREST);

            if (monitor != nint.Zero)
            {
                var monitorInfo = new Win32Api.MONITORINFO();
                monitorInfo.cbSize = Marshal.SizeOf(monitorInfo);

                // monitor의 사용 가능 영역 받아옴
                Win32Api.GetMonitorInfo(monitor, ref monitorInfo);

                PresentationSource source = PresentationSource.FromVisual(window);
                if (source?.CompositionTarget != null)
                {
                    // 현재 모니터의 배율 설정값 받아옴
                    double matrixX = source.CompositionTarget.TransformToDevice.M11;
                    double matrixY = source.CompositionTarget.TransformToDevice.M22;

                    // 윈도우는 최대화시 화면 밖으로 살짝 나가는 성질이 있어서 그대로 적용하면 우측 하단에 틈이 생김
                    // 그걸 방지하기위해 그 틈의 크기를 계산
                    double horizontalBorder = SystemParameters.WindowResizeBorderThickness.Left + SystemParameters.WindowResizeBorderThickness.Right;
                    double verticalBorder = SystemParameters.WindowResizeBorderThickness.Top + SystemParameters.WindowResizeBorderThickness.Bottom;

                    // 배율 적용, 우측 하단 틈 크기 적용
                    window.MaxHeight = Math.Abs(monitorInfo.rcWork.Bottom - monitorInfo.rcWork.Top) / matrixY + verticalBorder;
                    window.MaxWidth = Math.Abs(monitorInfo.rcWork.Right - monitorInfo.rcWork.Left) / matrixX + horizontalBorder;
                }
                else
                {
                    window.MaxHeight = Math.Abs(monitorInfo.rcWork.Bottom - monitorInfo.rcWork.Top) + 14;
                    window.MaxWidth = Math.Abs(monitorInfo.rcWork.Right - monitorInfo.rcWork.Left) + 14;
                }
            }
        }
        /// <inheritdoc/>
        public void Close(object viewModel)
        {
            GetWindowByViewModel(viewModel)?.Close();
        }
        /// <inheritdoc/>
        public void DragMove(object? obj)
        {
            if (obj is UIElement element)
            {
                Window window = Window.GetWindow(element);
                if (window == null || window.WindowState == WindowState.Minimized) return;

                try
                {
                    if (window.WindowState == WindowState.Maximized)
                    {
                        // 최대화 상태에서 마우스가 가로 전체 중 어디쯤 있는지 비율(0.0~1.0) 계산
                        double relativeX = Mouse.GetPosition(window).X / window.ActualWidth;
                        double relativeY = Mouse.GetPosition(window).Y;

                        // 현재 마우스의 절대적인 화면 좌표를 저장
                        Point screenMousePos = PointToScreen(window, Mouse.GetPosition(window));

                        // 창을 일반 모드로 변경
                        window.WindowState = WindowState.Normal;

                        // 좌표 재설정
                        window.Left = screenMousePos.X - window.ActualWidth * relativeX;
                        window.Top = screenMousePos.Y - relativeY;
                    }
                    if (Mouse.LeftButton == MouseButtonState.Pressed)
                    {
                        window.DragMove();
                    }
                }
                catch (InvalidOperationException ex)
                {
                    Debug.WriteLine($"DragMove 무시됨: {ex.Message}");
                }
            }
        }
        /// <summary>
        /// 현재 마우스가 어디에 위치하고있는지 좌표를 얻기위한 메서드
        /// </summary>
        private Point PointToScreen(Window window, Point point)
        {
            // 모니터의 설정 정보를 가져옴(현재 창이 몇번째 모니터에 존재하는지, 모니터의 배율 설정값 등)
            PresentationSource source = PresentationSource.FromVisual(window);

            // 모니터의 화면 구성을 담당하는 CompositionTarget이 준비됐는지 확인
            if (source?.CompositionTarget != null)
            {
                // 입력받은 마우스 좌표 값에 모니터 설정 정보를 적용해서 모니터상의 실제 좌표를 내보냄
                return source.CompositionTarget.TransformToDevice.Transform(point);
            }
            return point;
        }
        /// <summary>
        /// 메인 윈도우를 화면에 표시하고 활성화
        /// </summary>
        public void RestoreMainWindow()
        {
            // lock에 접근 가능하면 Processing 진행이 가능하니 true로 변경 후 메서드 실행
            lock (_lock)
            {
                if (_isProcessing) return;
                _isProcessing = true;
            }

            // window 창을 받아오지 못하면 무시
            Window window = Application.Current.MainWindow;
            if (window == null || !window.IsLoaded)
            {
                ReleaseProcessing();
                return;
            }

            // 작업 표시줄에 띄우기
            window.Show();
            window.ShowInTaskbar = true;
            // 스타일 적용이 끝나면 내부 코드 실행
            window.Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(() =>
            {
                window.WindowState = WindowState.Normal;
                window.Activate();
                window.Focus();

                ReleaseProcessing();
            }));
        }
        /// <summary>
        /// 프로그램 종료 명령
        /// </summary>
        public void ShutDown()
        {
            foreach (Window window in Application.Current.Windows.Cast<Window>().ToList())
            {
                window.Close();
            }
            Application.Current.Shutdown();
        }
        #endregion
        #region Window 창 띄우기
        /// <inheritdoc/>
        public bool? ShowDialog(BaseViewModel viewModel)
        {
            Window? window = null;

            // ViewModel의 타입에 따라 띄울 창을 결정함
            if (viewModel is TodoBaseViewModel todoVM)
            {
                window = new TodoWindow(todoVM);
            }
            else if (viewModel is ListWindowViewModel listVM)
            {
                window = new ListWindow(listVM);
            }
            else if (viewModel is SettingViewModel settingVM)
            {
                window = new SettingWindowView(settingVM);
            }

            if (window != null)
            {
                window.Owner = Application.Current.MainWindow;

                // 윈도우가 종료될때 구독중인 메세지 전부 해제
                window.Closed += (s, e) =>
                {
                    if (viewModel is WindowBaseViewModel winVM)
                    {
                        winVM.UnregisterMessages();
                    }
                };

                return window.ShowDialog();
            }

            return false;
        }
        /// <inheritdoc/>
        public bool? ShowEditWindow(object? obj, ITodoRepository todoRepository)
        {
            EditTodoViewModel? editVM = null;

            if (obj is ScheduleData schedule)
            {
                editVM = new EditTodoViewModel(todoRepository, schedule);
            }
            else if (obj is RoutineData routineData)
            {
                editVM = new EditTodoViewModel(todoRepository, routineData);
            }
            // RoutineInstance는 RoutineRecord를 상속받기때문에 여기서 실행
            else if (obj is RoutineRecord routineRecord)
            {
                editVM = new EditTodoViewModel(todoRepository, routineRecord);
            }

            return editVM != null ? ShowDialog(editVM) : false;
        }
        #endregion
    }
}

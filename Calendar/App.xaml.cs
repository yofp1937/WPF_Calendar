using Calendar.Common.Controller;
using Calendar.Common.Messages;
using Calendar.Common.Service;
using Calendar.Common.Util;
using Calendar.Manager;
using Calendar.Model;
using Calendar.View;
using System.Windows;

namespace TODO_List
{
    public partial class App : Application
    {
        private readonly DataManager _dataManager = new DataManager();
        private TrayIconController _trayIconController = null!;

        /// <summary>
        /// MainWindow.xaml의 폴더 위치를 바꿔도 문제없이 실행하기위해
        /// 프로젝트 실행시 Application_Startup을 호출하도록 변경
        /// </summary>
        private void Application_Startup(object sender, StartupEventArgs e)
        {
            // 앱이 비정상적으로 종료되면 입력된 함수를 호출하겠다
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            // 트레이 아이콘 초기화
            _trayIconController = new TrayIconController();

            // Messenger 구독 시작
            SubscribeMessenger();

            // HolidayProvider에 올해 공휴일을 생성해둠
            Task.Run(() => { HolidayProvider.InitTargetYaerHolidays(DateTime.Today.Year); });

            MainWindow window = new MainWindow(_dataManager, _dataManager);
            window.Show();
        }

        /// <summary>
        /// 프로그램이 비정상적으로 종료될때 호출
        /// </summary>
        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            WaitingForSavingData();
        }

        /// <summary>
        /// App.xaml.cs에서 구독 진행
        /// </summary>
        private void SubscribeMessenger()
        {
            Messenger.Subscribe<WindowMessages.OpenWindowMessage>(this, msg =>
            {
                if (msg.ViewModel != null)
                {
                    WindowService.Instance.ShowDialog(msg.ViewModel);
                }
                else if (msg.Obj != null)
                {
                    WindowService.Instance.ShowEditWindow(msg.Obj, _dataManager);
                }
            });
        }

        /// <summary>
        /// 프로그램이 종료될때 호출
        /// </summary>
        protected override void OnExit(ExitEventArgs e)
        {
            try
            {
                _trayIconController?.Dispose();
                WaitingForSavingData();
            }
            finally
            {
                base.OnExit(e);
            }
        }

        /// <summary>
        /// 최후의 데이터 저장
        /// </summary>
        private void WaitingForSavingData()
        {
            _dataManager.WaitingForSavingData();
        }
    }
}

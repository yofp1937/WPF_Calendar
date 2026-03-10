/*
 * 시스템 트레이에 나타나는 아이콘 컨트롤러
 */
using Calendar.Common.Service;
using System.IO;
using System.Windows.Resources;

namespace Calendar.Common.Controller
{
    public class TrayIconController : IDisposable
    {
        #region Property
        private readonly NotifyIcon _notifyIcon;
        #endregion

        #region 생성자
        public TrayIconController()
        {
            _notifyIcon = new NotifyIcon();
            Initialize();
        }
        #endregion

        #region 메서드
        /// <summary>
        /// 초기화
        /// </summary>
        private void Initialize()
        {
            SetIcon();
            ConfigureContextMenu();
            RegisterEvents();
        }
        /// <summary>
        /// 시각적 요소(아이콘, 프로그램 이름 등) 설정
        /// </summary>
        private void SetIcon()
        {
            // WPF는 배포시 exe 파일로 배포되는데 아이콘 파일을 exe 파일에서 꺼내쓰게끔 구성
            Uri iconUri = new Uri("pack://application:,,,/Resource/calendar.ico");
            StreamResourceInfo iconResource = Application.GetResourceStream(iconUri);
            if (iconResource != null)
            {
                using (Stream icon = iconResource.Stream)
                    _notifyIcon.Icon = new Icon(icon);
            }
            else
            {
                _notifyIcon.Icon = SystemIcons.Application;
            }
            _notifyIcon.Text = "Calendar";
            _notifyIcon.Visible = true;
        }
        /// <summary>
        /// 마우스 우클릭했을때 나오는 ContextMenu 설정
        /// </summary>
        private void ConfigureContextMenu()
        {
            // 아이콘 우클릭 이벤트 ContextMenu 생성
            ContextMenuStrip contextMenu = new ContextMenuStrip();
            contextMenu.Items.Add("창 열기", null, (s, e) =>
            {
                WindowService.Instance.RestoreMainWindow();
            });
            contextMenu.Items.Add("프로그램 종료", null, (s, e) =>
            {
                WindowService.Instance.ShutDown();
            });
            _notifyIcon.ContextMenuStrip = contextMenu;
        }
        /// <summary>
        /// 이벤트 핸들러 연결
        /// </summary>
        private void RegisterEvents()
        {
            // 아이콘 더블 클릭 이벤트 연결
            _notifyIcon.DoubleClick += (s, e) => WindowService.Instance.RestoreMainWindow();
        }
        // Interface 구현
        public void Dispose()
        {
            _notifyIcon.Visible = false;
            if(_notifyIcon.ContextMenuStrip != null)
                _notifyIcon.ContextMenuStrip.Dispose();
            _notifyIcon.Dispose();
        }
        #endregion
    }
}

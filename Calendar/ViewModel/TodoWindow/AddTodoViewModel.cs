/*
 * 일정, 규칙 등록을 위한 TodoViewModel
 */
using Calendar.Common.Interface;
using Calendar.Common.Messages;
using Calendar.Common.Util;
using Calendar.Model.DataClass.TodoEntities;
using Calendar.Model.Enum;

namespace Calendar.ViewModel.TodoWindow
{
    public class AddTodoViewModel : TodoBaseViewModel
    {
        #region 생성자, override
        public AddTodoViewModel(ITodoRepository todoRepository, bool isRoutine) : base(todoRepository)
        {
            IsAddMode = true;
            IsRoutine = isRoutine;
            MainBarTitleText = IsRoutine ? "규칙 추가" : "일정 추가";
            SelectedRoutineType = IsRoutine ? RoutineType.Daily : RoutineType.None;
        }

        /// <summary>
        /// 등록 버튼 눌리면 (일정/규칙) 타입에 따라 데이터 추출해서 JSON 형식으로 저장
        /// </summary>
        protected override void SubmitExecute(object? obj)
        {
            if (!CheckRequiredData())
                return;

            if (IsRoutine)
            {
                RoutineData routineData = ApplyRoutineData(new RoutineData());
                GeneratePastRecords(routineData);
                // AddRoutine_AsyncSave는 Task(작업 결과)를 반환하는 메서드이다
                // Task(작업 결과)를 반환하는 메서드는 메서드 앞에 await을 붙여 결과를 기다려야하는데
                // 프로그램에서는 백그라운드 저장을 명령하고 즉시 창을 닫아야하므로 await을 사용하지 않아야한다
                // 그래서 버림 변수(_)를 활용해서 Task(작업 결과)를 확인하지않겠다고 명확하게 표시하여 오류 메세지를 제거한다
                _ = TodoRepository.AddOrUpdateData_AsyncSave(routineData);
            }
            else
            {
                ScheduleData scheduleData = ApplyCommonAndStatusData(new ScheduleData());
                _ = TodoRepository.AddOrUpdateData_AsyncSave(scheduleData);
            }
            Messenger.Send(new TodoMessages.RefreshTodoUI());
            CloseWindow();
        }
        #endregion
    }
}

/*
 * 일정, 규칙을 저장하는 저장소의 역할을 하는 Class들이 구현해야하는 Interface
 */
using Calendar.Model.DataClass;
using Calendar.Model.DataClass.TodoEntities;

namespace Calendar.Common.Interface
{
    public interface ITodoRepository
    {
        /// <summary>
        /// 데이터 저장 및 업데이트 (성공 여부를 bool로 반환)
        /// </summary>
        Task<bool> AddOrUpdateData_AsyncSave<T>(T data) where T : class;

        /// <summary>
        /// 데이터 제거
        /// </summary>
        Task<bool> DeleteData_AsyncSave<T>(T data) where T : class;

        /// <summary>
        /// 기존 RoutineData는 제거하고, 새로운 RoutineData를 추가하는 메서드
        /// </summary>
        Task<bool> ReplaceRoutineData(RoutineData existingData, RoutineData newData);

        /// <summary>
        /// 현재 저장소를 보여줌
        /// </summary>
        TodoStorage GetTodoStorage();

        /// <summary>
        /// 3초 뒤 데이터 저장
        /// </summary>
        void RequestSaveAfter3Seconds();

        /// <summary>
        /// 비정상 종료일때 데이터 저장 대기
        /// </summary>
        void WaitingForSavingData();
    }
}

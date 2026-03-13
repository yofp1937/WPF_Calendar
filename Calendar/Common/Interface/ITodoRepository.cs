/*
 * ViewModel이 데이터에 접근하기위해 방문하는 객체가 구현해야하는 Interface
 * 
 * ITodoRepository를 구현하는 객체들은 아래의 기능들을 구현해야한다.
 * 1.유효성 검사
 * 2.ITodoStorage에 데이터 변경 요청
 * 3.파일을 Json 형태로 저장
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

        /// <summary>
        /// 임시 사용
        /// </summary>
        bool TempEditRoutineAndRegister(RoutineData existingData, RoutineData newData);
        /// <summary>
        /// 임시 사용
        /// </summary>
        bool TempAddNewRoutine(RoutineData routineData);
    }
}

/*
 * JSON 형식으로 저장할 데이터 틀
 */
using Calendar.Common.Interface;
using Calendar.Model.DataClass.TodoEntities;
using Calendar.Model.Enum;
using System.Diagnostics;
using System.Text.Json.Serialization;

namespace Calendar.Model.DataClass
{
    public class TodoStorage : ITodoStorage
    {
        #region Property
        // 외부에서 TodoStorage를 참조해야할때(ex: ListWindowViewModel) 외부에선 값을 변경할수없게 public IReadOnlyList를 사용
        [JsonInclude]
        private List<ScheduleData> _scheduleDatas = new();
        [JsonInclude]
        private List<RoutineData> _routineDatas = new();
        [JsonInclude]
        private List<RoutineRecord> _routineRecords = new();
        [JsonInclude]
        private DateTime _lastUpdated = DateTime.Today.Date;

        // IReadOnlyList는 Json으로 저장할 필요가 없기때문에 [JsonIgnore] 부착
        [JsonIgnore]
        public IReadOnlyList<ScheduleData> ScheduleDatas => _scheduleDatas;
        [JsonIgnore]
        public IReadOnlyList<RoutineData> RoutineDatas => _routineDatas;
        [JsonIgnore]
        public IReadOnlyList<RoutineRecord> RoutineRecords => _routineRecords;
        #endregion

        #region Method
        #region public Method
        /// <inheritdoc/>
        /// <remarks>_lastUpdated의 날짜가 오늘 이전이면 <see cref="ChangeStatusOfData"/>를 호출합니다.</remarks>
        public void CheckLastUpdated()
        {
            DateTime today = DateTime.Today;
            if( today > _lastUpdated)
            {
                ChangeStatusOfData();
            }
        }
        /// <inheritdoc cref="ITodoStorage.FindOriginalData{T}(T)"/>
        /// <remarks>내부 저장소 검색 메서드인 <see cref="FindOriginalDataInStorage{T}(T)"/>를 호출하여 원본을 반환합니다.</remarks>
        public T? FindOriginalData<T>(T data) where T : BaseTodoData 
        {
            return FindOriginalDataInStorage(data);
        }
        /// <inheritdoc cref="ITodoStorage.AddOrUpdateData{T}(T)"/>
        /// <remarks>data를 수정할땐 기존 Instance를 제거 후 새로운 Instance를 추가하는 방식을 사용합니다.<br/>
        /// data를 추가하기 전에 <see cref="FindOriginalDataInStorage{T}(T)"/>로 원본 객체를 먼저 찾은 후 원본이 있으면 제거 후 추가합니다.</remarks>
        public bool AddOrUpdateData<T>(T data, bool isNewRoutineData = false) where T : BaseTodoData
        {
            if (data == null)
                return false;

            // 1.저장소에 동일한 데이터가 존재하는지 검색
            var found = FindOriginalDataInStorage(data);
            // 2.데이터가 존재하면 기존 데이터 제거
            if(found != null)
            {
                bool removeComplete = RemoveDataInStorage(found);
                if (!removeComplete)
                    return false;
            }
            // 3.데이터 추가
            AddDataInStorage(data, isNewRoutineData);

            return true;
        }
        /// <inheritdoc/>
        /// <remarks>RoutineData의 삭제 요청이 들어올 경우 <see cref="CloseOrRemoveRoutineData(RoutineData)"/>를 호출해 종료시킬지 삭제할지 판단합니다.</remarks>
        public bool RemoveData<T>(T data) where T : BaseTodoData
        {
            if (data == null)
                return false;
            return RemoveDataInStorage(data);
        }
        #endregion
        #region private Method
        /// <summary>
        /// _lastUpdated 날짜가 오늘 이전이면 과거 TodoData들의 Status를 Failure로 변경합니다.
        /// </summary>
        private void ChangeStatusOfData()
        {
            DateTime today = DateTime.Today;

            // 일정 중 오늘 이전의 날짜이면서 완료되지 않은 것 실패 처리
            foreach (ScheduleData schedule in _scheduleDatas.Where(s => s.StartDate.Date < today && s.Status == TodoStatus.Waiting))
            {
                schedule.Status = TodoStatus.Failure;
            }
            // 규칙 중 오늘 이전의 날짜이면서 완료되지 않은 것 실패 처리
            foreach (RoutineRecord record in _routineRecords.Where(r => r.Date.Date < today && r.Status == TodoStatus.Waiting))
            {
                record.Status = TodoStatus.Failure;
            }

            _lastUpdated = today;
        }
        #region Data 검색
        /// <summary>
        /// data와 동일한 데이터 타입의 데이터가 저장된 List를 순회하며<br/>
        /// FirstOrDefault를 사용해 Id가 일치하는 객체를 찾아 반환합니다.
        /// </summary>
        /// <param name="data">실제 저장소에서 찾고싶은 data</param>
        /// <returns>일치하는 원본 데이터, 없을 경우 null</returns>
        private T? FindOriginalDataInStorage<T>(T data) where T : BaseTodoData
        {
            if (data == null) return null;

            BaseTodoData? found = data switch
            {
                ScheduleData => _scheduleDatas.FirstOrDefault(x => x.Id == data.Id),
                RoutineData => _routineDatas.FirstOrDefault(x => x.Id == data.Id),
                RoutineRecord => _routineRecords.FirstOrDefault(x => x.Id == data.Id),
                _ => null
            };

            // data.Id를 기반으로 찾아낸 data를 T로 형변환하여 반환
            return found as T;
        }
        #endregion
        #region Data 추가
        /// <summary>
        /// data를 타입에 맞는 내부 저장소에 추가합니다.<para/>
        /// RoutineData를 추가하는 경우에는 <see cref="GeneratePastRoutineRecords(RoutineData)"/>를 호출하여 RoutineRecords를 생성할지 결정합니다.
        /// </summary>
        /// <param name="data">추가할 data</param>
        /// <param name="isNewRoutineData">새롭게 RoutineData를 추가하는지 여부(수정이면 false로 줘야합니다.)</param>
        private void AddDataInStorage(BaseTodoData data, bool isNewRoutineData = false)
        {
            switch (data)
            {
                case ScheduleData s:
                    _scheduleDatas.Add(s);
                    break;
                case RoutineData r:
                    _routineDatas.Add(r);
                    GeneratePastRoutineRecords(r, isNewRoutineData);
                    break;
                case RoutineRecord rr:
                    _routineRecords.Add(rr);
                    break;
            }
        }
        /// <summary>
        /// RoutineData를 저장소에 추가할때 StartDate를 검사해서 오늘 이전이면 StartDate ~ 어제까지의 RoutineRecords를 Status = Failure로 생성합니다.<br/>
        /// </summary>
        /// <param name="data">과거 RoutineRecord를 생성할 RoutineData</param>
        /// <param name="setFailure">True면 Status를 Failure로 설정, False면 Status를 Waiting으로 설정</param>
        private void GeneratePastRoutineRecords(RoutineData data, bool setFailure)
        {
            if(data == null) return;

            // 1.시작 날짜가 오늘이후면 return합니다.
            DateTime startDate = data.StartDate.Date;
            DateTime yesterday = DateTime.Today.AddDays(-1);
            if (startDate > yesterday) return;

            // 2.과거 시작 날짜부터 어제까지 하루씩 증가하며 Record를 생성합니다.
            for (DateTime date = startDate; date <= yesterday; date = date.AddDays(1))
            {
                // 2-1.해당 날짜가 규칙에 해당되지않으면 건너뜁니다.
                if (!data.IsCheckInDay(date)) continue;

                RoutineRecord record = new RoutineRecord(data, date);

                // 2-2.setFailure가 true면(신규 추가인 RoutineData일 경우) Status를 Failure로 설정합니다.(기본값 Waiting)
                if (setFailure)
                    record.Status = TodoStatus.Failure;

                _routineRecords.Add(record);
            }
        }
        #endregion
        #region Data 제거
        /// <summary>
        /// data를 타입에 맞는 내부 저장소에서 삭제합니다.
        /// </summary>
        /// <remarks>RoutineData의 삭제 요청이 들어올 경우 <see cref="CloseOrRemoveRoutineData(RoutineData)"/>를 호출해 종료시킬지 삭제할지 판단합니다.</remarks>
        /// <param name="data">삭제할 data</param>
        /// <returns>데이터 삭제 성공 시 True, 실패 시 False</returns>
        private bool RemoveDataInStorage(BaseTodoData data)
        {
            bool result = false;
            switch (data)
            {
                case ScheduleData s:
                    result = _scheduleDatas.Remove(s);
                    break;
                case RoutineData r:
                    result = CloseOrRemoveRoutineData(r);
                    break;
                case RoutineRecord rr:
                    result = _routineRecords.Remove(rr);
                    break;
            }
            return result;
        }
        /// <summary>
        /// RoutineData를 삭제하기 전 EndDate를 어제로 변경하고 종료시킬지, 저장소에서 삭제할지 결정합니다.<para/>
        /// 1.자신을 참조하는 RoutineRecord중 하나라도 Status값이 Waiting이 아니면 종료<br/>
        /// 2.자신을 참조하는 모든 RoutineRecord의 Status값이 Waiting일 경우 저장소에서 삭제
        /// </summary>
        /// <param name="data">종료하거나 제거시킬 RoutineData</param>
        /// <returns>종료나 삭제에 성공 시 True, 실패 시 False</returns>
        private bool CloseOrRemoveRoutineData(RoutineData data)
        {
            try
            {
                // 원본 데이터를 찾아둠
                RoutineData? target = FindOriginalData(data);
                if (target == null) return false;

                // 1.넘겨받은 RoutineData를 참조하는 RoutineRecord중 Status값이 Waiting이 아닌 값이 하나라도 존재하는지 확인
                bool hasRecords = RoutineRecords.Any(r => r.ParentRoutineId == target.Id && r.Status != TodoStatus.Waiting);
                if (hasRecords)
                {
                    // 2.하나라도 존재하면 RoutineData는 어제부로 종료시킴
                    target.IsIndefinite = false;
                    target.EndDate = DateTime.Today.AddDays(-1);
                }
                else
                {
                    // 3.Status가 전부 Waiting일 경우 저장소에서 제거
                    _routineDatas.Remove(target);
                }
                ClearUnusedRecords(data);
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[TodoStorage - CloseOrRemoveRoutineData]: 오류 발생 {ex}");
                return false;
            }
        }
        /// <summary>
        /// 넘겨받은 RoutineData를 참조하는 RoutineRecord중 Status가 Waiting인 인스턴스를 전부 제거합니다.
        /// </summary>
        /// <param name="data">Status가 Waiting인 Record를 제거하고싶은 RoutineData</param>
        private void ClearUnusedRecords(RoutineData data)
        {
            // RoutineRecord.ParentId가 data.Id와 같고, Status가 Waiting인 RoutineRecords를 한곳에 모음
            List<RoutineRecord> unusedRecords = RoutineRecords.Where(r => r.ParentRoutineId == data.Id &&
                                                                                               r.Status == TodoStatus.Waiting).ToList();
            // RoutineRecords를 저장소에서 제거
            foreach (RoutineRecord record in unusedRecords)
            {
                _routineRecords.Remove(record);
            }
        }
        #endregion
        #endregion
        #endregion
    }
}
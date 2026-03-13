/*
 * JSON 형식으로 저장할 데이터 틀
 */
using Calendar.Common.Interface;
using Calendar.Model.DataClass.TodoEntities;
using Calendar.Model.Enum;

namespace Calendar.Model.DataClass
{
    public class TodoStorage : ITodoStorage
    {
        public List<ScheduleData> Schedules { get; set; } = new();
        public List<RoutineData> Routines { get; set; } = new();
        public List<RoutineRecord> RoutineRecords { get; set; } = new();
        public DateTime LastUpdated { get; set; } = DateTime.Today.Date;

        /// <summary>
        /// 전달받은 객체의 타입에 따라 FirstOrDefault를 사용하여<br/>
        /// 해당 List에서 Id가 일치하는 원본 데이터를 찾아서 반환해주는 메서드
        /// </summary>
        public BaseTodoData? FindOriginal(BaseTodoData data)
        {
            return data switch
            {
                ScheduleData => Schedules.FirstOrDefault(x => x.Id == data.Id),
                RoutineData => Routines.FirstOrDefault(x => x.Id == data.Id),
                RoutineRecord => RoutineRecords.FirstOrDefault(x => x.Id == data.Id),
                _ => null
            };
        }

        /// <summary>
        /// 넘겨받은 RoutineData를 참조하는 유의미한 RoutineRecord가 존재하는지 검사한 후<br/>
        /// 유의미한 데이터가 존재하면 EndDate를 어제로 변경한 후 저장하고<br/>
        /// 유의미한 데이터가 없으면 RoutineData 자체를 제거
        /// </summary>
        public void FinishedOrRemoveRoutineData(RoutineData data)
        {
            // 넘겨받은 RoutineData를 참조하는 RoutineRecord중 Status값이 Waiting이 아닌 값이 존재하는지 확인
            bool hasRecords = RoutineRecords.Any(r => r.ParentRoutineId == data.Id && r.Status != Enum.TodoStatus.Waiting);

            // Id를 기반으로 RoutineData를 정확하게 찾아둠
            RoutineData? targetData = Routines.FirstOrDefault(r => r.Id == data.Id);
            if (targetData == null) return;

            if (hasRecords)
            {
                targetData.IsIndefinite = false;
                targetData.EndDate = DateTime.Today.AddDays(-1);
            }
            else
            {
                Routines.Remove(targetData);
            }
            ClearGarbageRecords(targetData);
        }

        /// <summary>
        /// 넘겨받은 RoutineData를 참조하는 RoutineRecord중 Stauts 값이 Waiting인 모든 RoutineRecords 제거
        /// </summary>
        public void ClearGarbageRecords(RoutineData data)
        {
            // RoutineData의 Id와 똑같고, Status가 Waiting인 RoutineRecords를 한곳에 모음
            List<RoutineRecord> garbageRecords = RoutineRecords.Where(r => r.ParentRoutineId == data.Id &&
                                                                                               r.Status == TodoStatus.Waiting).ToList();
            // RoutineRecords를 저장소에서 제거
            foreach (RoutineRecord record in garbageRecords)
            {
                RoutineRecords.Remove(record);
            }
        }

        /// <summary>
        /// routine을 저장소에 저장하고, 시작 날짜가 오늘 이전이면 시작 날짜부터 어제까지의 과거 기록을 자동으로 생성함<para/>
        /// setFailure가 true면 Status를 Failure로 변경
        /// </summary>
        public void AddRoutineWithPastRecords(RoutineData routineData, bool setFailure)
        {
            // 중복 규칙이 존재하지않게 검사 후에 추가
            if(!Routines.Contains(routineData))
            {
                Routines.Add(routineData);
            }

            // 규칙의 시작일이 오늘 이후면 return
            DateTime startDate = routineData.StartDate.Date;
            DateTime yesterday = DateTime.Today.AddDays(-1);
            if (startDate > yesterday) return;

            // 시작 날짜부터 어제까지 하루씩 증가하며 검사
            for (DateTime date = startDate; date <= yesterday; date = date.AddDays(1))
            {
                // 해당 날짜가 규칙에 해당되지않으면 건너뛰기
                if (!routineData.IsCheckInDay(date)) continue;

                RoutineRecord record = new RoutineRecord(routineData, date);
                // setFailure가 true면 Status를 Failure로 설정
                if (setFailure) record.Status = TodoStatus.Failure;

                RoutineRecords.Add(record);
            }
        }
    }
}

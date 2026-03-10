/*
 * 규칙 추가에서 사용하는 데이터 타입
 * 
 * RoutineData는 반복적으로 데이터를 생성해야한다 (RoutineData를 기반으로 날짜마다 RoutineInstance를 생성하여 Days에 할당)
 * 
 * RoutineData, RoutineRecord의 수정 기준
 * 1. 과거의 Routine을 수정하면 RoutineRecord를 수정하고,
 *    오늘 Routine을 수정할때 Title, Content를 수정하면 RoutineRecord를 그 외의 값을 수정하면 RoutineData를 수정하며,
 *    미래의 Routine을 수정하면 RoutineData를 수정한다.
 * 2. RoutineData의 StartDate와 EndDate는 DateTime.Today 이전으로 수정할 수 없다.
 * 3. RoutineData에서 선언된 데이터를 변경했으면 새로운 RotuieData를 생성하고 기존 RoutineData는 남겨둔다.
 *    3-1. 기존 RoutineData는 삭제하지않고 EndDate에 DateTime.Today.AddDays(-1);를 넣고 저장하고, (오늘 날짜의 규칙은 삭제해야할듯)
 *          이후 수정된 데이터는 StartDate를 입력받은 값으로 적용하여 생성한다.
 *          이렇게하면 수정 전 데이터의 RoutineInstance에 접근했을때 수정 전 TodoText, TodoDetail을 확인할 수 있고, 수정 후 데이터도 정상 작동한다.
 *    
 * Routine Data 수정 완료시
 * 1. 기존 RoutineData의 EndDate를 변경한다.
 * 2. RoutineRecords를 순회하면서 RoutineData의 Id로 생성된 Records중 IsCompleted, IsFailure가 둘다 false면 해당 RoutineRecord는 삭제한다.
 * 3. 수정하며 새로 만들어진 RoutineData를 저장한다.
 */
using Calendar.Model.Enum;

namespace Calendar.Model.DataClass.TodoEntities
{
    public class RoutineData : BaseTodoData
    {
        public RoutineType RoutineType { get; set; } // 일간, 주간, 월간, 연간 구분
        public int Frequency { get; set; } // 주기 (n일마다, n주마다)
        public bool IsIndefinite { get; set; } // 기한 없음 체크
        public DateTime? EndDate { get; set; } // 종료 날짜

        // --- 주간, 월간, 연간 리스트 ---

        // 1. 주간: 일~토 (0~6) 중 선택된 요일들
        public List<DayOfWeek>? SelectedWeeklyDays { get; set; }

        // 2. 월간: 1~31일 중 선택된 날짜들
        public List<int>? SelectedMonthlyDates { get; set; }

        // 3. 연간: 특정 월/일의 조합 (중복 가능하므로 DateTime의 리스트로 관리)
        // 연도 정보는 무시하고 월/일만 사용
        public List<DateTime>? SelectedYearlyDates { get; set; }

        #region 메서드
        /// <summary>
        /// 기존 루틴의 설정을 이어받는 새로운 루틴을 생성
        /// </summary>
        public static RoutineData CreateCopiedRoutineData(RoutineData oldData)
        {
            return new RoutineData
            {
                Id = Guid.NewGuid(),
                TodoTitle = oldData.TodoTitle,
                TodoContent = oldData.TodoContent,
                StartDate = DateTime.Today,
                RoutineType = oldData.RoutineType,
                Frequency = oldData.Frequency,
                IsIndefinite = oldData.IsIndefinite,
                EndDate = oldData.EndDate,

                // 리스트 데이터 복사 (참조를 공유하지 않도록 새로 생성)
                SelectedWeeklyDays = oldData.SelectedWeeklyDays != null ? new List<DayOfWeek>(oldData.SelectedWeeklyDays) : null,
                SelectedMonthlyDates = oldData.SelectedMonthlyDates != null ? new List<int>(oldData.SelectedMonthlyDates) : null,
                SelectedYearlyDates = oldData.SelectedYearlyDates != null ? new List<DateTime>(oldData.SelectedYearlyDates) : null
            };
        }

        /// <summary>
        /// targetDate가 현재 Routine에 포함되는지 검사<br/>
        /// 포함되면 true, 포함 안되면 false
        /// </summary>
        public bool IsCheckInDay(DateTime targetDate)
        {
            // 범위 밖이면 탈락
            if (targetDate < StartDate || !IsIndefinite && targetDate > EndDate)
                return false;

            // 시작일로부터 얼마나 지났는지 계산
            TimeSpan diff = targetDate - StartDate;

            switch (RoutineType)
            {
                case RoutineType.Daily:
                    return diff.Days % Frequency == 0;
                // 주간: 빈도뿐만 아니라 '요일'도 체크해야 함
                case RoutineType.Weekly:
                    bool isCorrectWeek = diff.Days / 7 % Frequency == 0;
                    bool isSelectedDay = SelectedWeeklyDays?.Contains(targetDate.DayOfWeek) ?? false;
                    return isCorrectWeek && isSelectedDay;
                // 월간: 개월 수 차이 계산
                case RoutineType.Monthly:
                    int monthDiff = (targetDate.Year - StartDate.Year) * 12 + targetDate.Month - StartDate.Month;
                    bool isCorrectMonth = monthDiff % Frequency == 0;
                    bool isSelectedMonthDay = SelectedMonthlyDates?.Contains(targetDate.Day) ?? false;
                    return isCorrectMonth && isSelectedMonthDay;
                // 연간: 특정 날짜와 일치하는지 확인
                case RoutineType.Yearly:
                    int yearDiff = targetDate.Year - StartDate.Year;
                    bool isCorrectYear = yearDiff % Frequency == 0;
                    // 여길 selectedYearlyDates의 날짜들과 비교하여 일치하는지 검사하게 바꿔야함
                    bool isSameDay = SelectedYearlyDates?.Any(d => d.Month == targetDate.Month && d.Day == targetDate.Day) ?? false;
                    return isCorrectYear && isSameDay;
            }
            return false;
        }
        #endregion
    }
}
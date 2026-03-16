# WPF_Calendar
일정, 루틴 데이터를 입력하면 달력 UI를 통해 매일, 매주, 매달 해야 할 일을 간단하게 확인하고 완료 여부를 관리할 수 있는 프로그램


# 개발 기간
2025.12.01 ~ 26.03.07 (ver 1.0 - 기본 기능 구현 완료)


# 사용 기술
C#, WPF, MVVM, JSON Serialization

# 프로젝트 아키텍처

 ### ① 전체 흐름도
<img width="4044" height="1764" alt="Image" src="https://github.com/user-attachments/assets/67235af4-fe54-4625-bb3c-392240e43df6" />

 ### ② 주요 데이터 흐름
 <img width="3484" height="1272" alt="Image" src="https://github.com/user-attachments/assets/0d27ed1c-e477-4b41-a909-35d2a9a7f716" /><br/>
 - ViewModel: 사용자가 입력한 Binding Data를 기반으로 Data를 생성하여 ITodoRepository에 처리를 요청<br/>
 - ITodoRepository: ViewModel에게 전달받은 Data와 요청을 ITodoStorage로 전달하고,<br/>
 ITodoStorage의 처리 결과에따라 Storage의 상태를 Json 형태로 Local Repository에 저장함<br/>
 - ITodoStorage: ITodoRepository에게 전달받은 Data를 요청에따라 처리하고 결과를 반환해줌

# 주요 기능과 데이터 흐름 설명

 ### ① MVVM 패턴 사용
  - 새로운 창을 띄울때 ViewModel에서 직접 생성하지않고 Messenger를 통해 생성 메세지를 전달하면<br/>
  WindowService 클래스가 메세지를 수신하여 창을 띄워주게 설계
  - Interface를 활용하여 상위 객체가 하위 객체의 정확한 데이터 타입을 몰라도 공통 메서드를 호출할수있게 설계
  - 규칙(RoutineData, RoutineRecord)을 View에 표시할떄 UI 전용 Wrapper 객체인 RoutineInstance를 사용해<br/>
  View는 RoutineInstance에만 접근하면 되게끔 설계

 ### ② 사용자 UI와 편의성
  - 프로그램을 전체화면으로 변경할때 작업 표시줄까지 가리지않게 구현
  - 최소화시 SystemTray로 이동시키는 기능과 해당 옵션을 사용자가 선택할수있게 구현
  - OS 부팅시 프로그램이 자동으로 실행되는 기능과 해당 옵션을 선택할수있게 구현

 ### ③ 데이터 저장 및 안정성
  - 데이터를 처리할때 UI가 멈추는 현상을 방지하기위해 async/await을 사용해 데이터를 비동기로 처리하게 구현
  - CancellationTokenSource를 사용하여 3초동안 입력이 멈추면 데이터를 처리하게 만들어 성능 최적화
  - 데이터 저장시 .tmp 파일로 임시 저장한뒤 원본 파일과 교체하여 데이터 파손을 방지
---

## 달력

 #### ① 달력 생성 데이터 흐름도 (CalendarViewModel)
 <img width="4204" height="2848" alt="Image" src="https://github.com/user-attachments/assets/d1b42554-6bda-446e-9e14-1cbe6e8e81a8" />
 
 - 달력의 연도가 바뀌면 HolidayProvider로 해당 연도의 공휴일을 생성
 - 달력을 그릴때마다 우선적으로 첫주 ~ 마지막주까지 날짜를 생성 날짜는 CalendarDayModel이라는 Class로 구현해서 정보(날짜, 휴일 여부, 해당 날짜의 일정 등)를 저장
 - **날짜 생성 이후 날짜마다 표시해야할 일정, 규칙이 있는지 확인하고 등록**

 #### ② 달력 기능
https://github.com/user-attachments/assets/4d35c900-419c-4bdc-8624-c8832831fd04

 - 달력의 날짜를 클릭하면 해당 날짜의 일정이 왼쪽 패널에 표시되고 체크박스로 완료 여부를 변경할 수 있음
 - 체크박스를 체크하여 데이터의 상태(완료, 실패, 대기)를 변경하면 즉시 저장하지 않고 3초동안 입력이 없을때 저장함(값을 연속해서 바꾸면 마지막에 한번만 저장)
 - 한 날짜의 일정, 규칙 갯수가 7개를 넘어가면 맨 아래 표시되는 일정 텍스트가 ...(+n)으로 변경됨
 
 #### ③ 달력의 주요 Item ViewModel인 CalendarDayModel 데이터 흐름도
 <img width="3564" height="1724" alt="Image" src="https://github.com/user-attachments/assets/81073291-443d-43a5-b3f3-479d06c499f8" />
 
 - CalendarDayModel은 달력의 날짜 하루당 하나씩 생성함
 - CalendarViewModel에서 CalendarDayModel이 생성되면 생성자에서 날짜, 요일, 휴일 여부 등 기본적인 데이터를 자동으로 설정함
 - SidePanelTasksView에서 선택된 날짜의 일정, 규칙을 표시하기위해 CalendarDayModel의 AllTasksView를 Binding
 - **생성자에서 일정, 규칙 List에 Data가 추가될 경우 자동으로 AllTasksView 정렬시키고 OnPropertyChanged를 호출해 UI에도 갱신되게 이벤트 등록**
 - 외부에서 AllTasksView에 접근하면 반환해주는데, 최초 접근일경우 정렬 설정을 적용하여 Instance를 생성하고 반환함

 #### ④ 특정 연도의 공휴일을 계산하는 HolidayProvider 데이터 흐름도
 <img width="4248" height="2848" alt="Image" src="https://github.com/user-attachments/assets/1103b793-af6d-47ea-8ef3-b1117b06856d" />
 
 - 음력 공휴일은 .NET 프레임워크에 내장된 KoreanLunisolarCalendar 클래스를 활용해 구현
 - 달력의 연도가 바뀌면 HolidayProvider에서 해당 연도의 공휴일과 임시 공휴일을 계산하여 가지고있고,<br/>
 CalendarDayModel에서 접근해 자신이 휴일인지 확인함

### 일정, 규칙(반복 일정)
  - 프로그램 실행 시 저장소의 LastUpdate가 오늘 이전의 날짜로 저장돼있으면 오늘 이전 날짜들의 일정, 규칙 Status(상태) 값을 Failure(실패)로 변경함
 #### ① 일정
  - ScheduleData라는 DataClass로 관리하고 세부적으론 일정의 제목, 내용, 날짜 정보를 관리함

 #### ② 규칙(반복 일정) 설계 (RoutineData, RoutineRecord, RoutineInstance)
  - 규칙은 생성시 RoutineData라는 DataClass로 관리하고 세부적으론 제목, 내용, 시작 날짜, 종료 날짜, 주기 등 규칙 생성에관한 중요 정보를 관리함
  - 위 RoutineData를 기반으로 RoutineRecord라는 DataClass를 생성하여 규칙을 생성하고 저장, 관리함 (과거 규칙은 RoutineRecord 형태로 저장소에 저장하여 RoutineData가 수정, 삭제되도 과거 기록은 보존될수있게 설계)
  - 수백, 수천개에 달할수있는 미래 규칙은 미리 저장하지않고 달력을 그리는 시점에 실시간으로 계산해 UI에 표시
  - 달력에 규칙을 표시할땐 RoutineInstance라는 DataClass를 생성하여 View에서는 RoutineInstance에만 접근하여 바인딩하면 되게끔 설계
  ##### ⓐ RoutineData - 규칙의 설계도로 기본 정보와 반복 주기 데이터를 포함하며 TodoStorage에 저장되어 관리함
  ##### ⓑ RoutineRecord - 특정 날짜의 실행 결과로 규칙의 성공/실패 상태를 저장하며, 원본 규칙이 변경되어도 과거 기록은 보존되도록 독립된 스냅샷 정보를 유지함
  ##### ⓒ RoutineInstance - UI 표현을 위한 가상 모델로 과거 규칙은 Record 기반으로, 미래 규칙은 Data 기반으로 데이터를 결합해 View에 일관된 데이터를 제공함
   
### 일정, 규칙 등록

https://github.com/user-attachments/assets/aa66ac2e-1992-4e67-8e5b-05bcceea4bd7

 #### ① 일정, 규칙 등록 및 데이터 흐름도
 <img width="4364" height="2928" alt="Image" src="https://github.com/user-attachments/assets/92d79dcb-76b5-4577-8721-3ca182c4044c" />
 
 - 데이터를 입력하고 등록 버튼을 누르면 ViewModel에서 데이터를 기반으로 Instance를 생성
 - ITodoRepository에 데이터 추가 요청을 Instance와 함께 전송하면 ITodoRepository는 이를 ITodoStorage로 그대로 전달하고 처리 결과 대기
 - ITodoStorage는 내부에서 분기에따라 Instance를 처리함
 - RoutineData의 StartDate가 과거일 경우 StartDate부터 어제까지의 RoutineRecord를 미리 "실패 상태"로 생성하여 저장소에 저장
 - 처리한 결과를 ITodoRepository에 전달하고 ITodoRepository는 변경된 ITodoStorage를 LocalRepository에 저장
 - ViewModel에서는 LocalRepository에 데이터 저장까지 끝나면 Messenger에게 UI 갱신 메세지 전송

### 일정, 규칙 수정

https://github.com/user-attachments/assets/7aff627a-3c94-46ff-9863-9ff90022f27f

 #### ① 수정 기능
 - 데이터를 수정할땐 날짜를 선택하고 왼쪽 패널의 목록에서 텍스트를 더블 클릭하여 수정 창을 열수있음
 - 수정 창에서 값을 변경하고 수정 버튼을 클릭시 참조중인 데이터의 값을 수정하고 DataManager에 저장 요청을 보냄

 #### ② 수정 데이터 흐름도
 <img width="801" height="1041" alt="Routine 수정 데이터 흐름도" src="https://github.com/user-attachments/assets/692391af-87d3-43bd-ba87-0fdc57a71fbd" />
 
### 일정, 규칙 삭제

https://github.com/user-attachments/assets/9d19567c-c5c7-4b96-95ba-42435b45f296

 #### ① 삭제 기능
 - 일정이나 규칙을 삭제할땐 수정 창을 열어서 삭제 버튼을 눌러 삭제가 가능함
 - Window에서 삭제 버튼을 누르면 TodoStorage에서 해당 데이터를 삭제하고 DataManager에 데이터 저장 요청을 보냄
 - RoutineData를 삭제할때 자신을 참조하여 생성된 RoutineRecord중 Status값이 Waiting인 RoutineRecord들을 쓰레기 값으로 판단하고 전부 제거함<br/>
   (RoutineData를 수정하여 신규 RoutineData가 생성됐을떄 Status를 Waiting으로 설정하여 생성하는데 잘못 수정했을떄 삭제하기 쉽게끔 설계)

### 일정, 규칙 목록

https://github.com/user-attachments/assets/77210e47-88be-403d-bb5a-697503a149d4

 #### ① 목록 기능
 - 목록 버튼을 눌러서 등록한 모든 일정과 규칙을 확인하고 삭제, 수정 할 수 있음
 - 상단 탭으로 일정, 규칙, 과거 규칙 세가지 형태의 데이터에 접근이 가능
 - 체크박스를 선택해 삭제할 일정을 여러개 선택할 수 있음
 - 체크박스 외의 흰 바탕 부분을 클릭하면 일정이 파란색으로 선택되는데 수정 버튼을 누르면 파란색으로 선택된 데이터를 수정할 수 있음 (더블 클릭으로도 수정 가능)

---

## 설정

https://github.com/user-attachments/assets/b89b817f-09d5-47d6-ac11-f1df0a4c0d08

 - 설정 버튼을 눌러서 프로그램 설정을 변경할 수 있음 (부팅시 프로그램 자동 실행, 최소화시 시스템 트레이로 이동)
 - 프로그램 자동 실행을 키면 AutoStartService에서 Registry에 접근해 현재 사용자의 권한으로 자동 실행을 등록함
 - 최소화시 시스템 트레이로 이동을 키면 프로그램 전역에서 Messenger를 통해 WindowService의 Minimize를 호출할 경우 MainWindow를 작업 표시줄에서 안보이게 변경하고 Alt + Tab을 눌러도 보이지 않게 Hide()로 상태를 변경함

---

# 만들며 어려웠던 점과 어떻게 해결했는지 (문제 발생과 원인 분석, 해결의 자세한 내용은 문서 폴더의 문제 해결.txt에 기술)
 ### ① WPF ItemsControl의 렌더링 시점에 따른 트리거 오작동 (/View/Calendar/CalendarView.xaml)
  [문제] CalendarView의 ItemsControl 내부에서 MultiDataTrigger로 AlternationCount를 사용해 하위 객체에 부여된 AlternationIndex 값에 따라 데이터를 변경하려 했으나 모든 하위 객체의 AlternationIndex가 0으로 인식됨
  
  [원인] WPF의 ItemControls 렌더링 생명주기상 MultiDataTrigger의 적용 시점이 AlternationCount를 부여하는 단계보다 빠르기 때문에 초기 로드시 AlternationIndex값을 정상적으로 참조하지 못하는것을 확인

  [해결] MultiDataTrigger는 부모 요소의 속성 변화를 제대로 감지하지 못하기에 동일 계층의 Grid.Tag를 중간 매개체로 활용해 Grid.Tag에 AlternationCount값을 바인딩하여 값의 변화를 MultiDataTrigger가 즉시 감지할수있도록 우회
  
 ### ② 파일 미종료로 인한 Json 저장 실패 (/Common/Util/FileHelper.cs)
  [문제] FileHelper에서 데이터를 Json 형식으로 저장하려했는데, 기존 Json 데이터만 남아있고 변경 사항이 반영되지 않음

  [원인] LoadJson 과정에서 파일을 읽어온 후 닫지 않아서 다른 프로세스(저장 로직)가 해당 파일에 접근할때 '파일 쓰기 권한' 충돌 발생

  [해결] using 블록을 이용해 Json 데이터를 읽은 후 확실하게 파일을 종료하여 저장 로직이 접근 권한을 즉시 확보할수 있도록 수정

 ### ③ 메서드 충돌로 인해 'System.InvalidOperationException'(PresentationCore.dll)' 예외 발생
  [문제] 커스텀 타이틀바의 최소화 버튼 클릭시 'System.InvalidOperationException'(PresentationCore.dll)' 발생 및 프로그램 정지

  [원인] 최소화 버튼 클릭시 WindowBehavior.DragMove와 WindowService.Minimize 요청이 동시에 발생하여 윈도우 상태 변경 로직이 충돌함

  [해결] 마우스 Pressed 상태일 때만 DragMove가 동작하도록 방어 코드 작성, WindowBehaivor에도 추가 방어 코드 작성

 ### ④ 프로그램 종료 시 비동기 메서드의 동기적 호출로 인한 데드락 발생 (/App.xaml.cs (OnExit))
  [문제] 프로그램 종료 후에도 프로세스가 소멸되지않고 메모리를 점유함

  [원인] OnExit에서 비동기 저장 메서드를 GetResult()를 사용하여 호출하니 UI 스레드가 비동기 작업 완료 신호를 기다리는 동안 작업은 완료 후 복귀할 UI 스레드가 차단되어있어 서로 무한 대기하는 데드락 발생

  [해결] Task.Run을 사용해 비동기 작업 자체를 백그라운드 스레드로 위임하여 UI 스레드와의 의존성을 분리함

---

# 만들며 아쉬웠던 점
 ### ① 세부 설계 기획의 부재로 인한 개발 시간 증가
  - 화면 기획서만 작성하고 데이터 흐름, 객체간 상속 구조, 조건부 로직 등 세부 동작 설계서 없이 개발에 착수했더니 Routine을 설계할때 많은 시간이 걸림 (만들고 수정하고 또 수정하고..)
  - 앞으로 순서도나 클래스별 용도와 이름, 핵심이 되는 로직의 방향성 등은 제대로 기획서를 작성하고 개발을 착수해야겠다고 느낌 (나중에 주석만 봐도 한번에 이해할수있게 주석도 상세하게 잘 달아놓기)

 ### ② 추상화 및 다형성 활용의 미흡
  - 인터페이스를 사용하여 결합도를 낮춘 설계를 만들고자 했는데 DataManager같은 핵심 로직에서 switch문을 사용해 하위 객체의 구체적인 타입을 직접 참조하고있음
  - 다형성과 여러가지 패턴들을 공부하여 필요한 상황에 적절하게 사용할수있게 만들어야할거같음

---

# 추가로 수정할 것
 ### ① RoutineRecord가 많아지면 메모리 점유율이 높아질수있기에 RoutineRecord는 RoutineRecords 폴더를 만들어서 yyyy_MM.json 형태로 월별로 저장하기
  - 이에따라 TodoStorage의 Records에는 현재 표시중인 달의 RoutineRecords만 저장하게 변경하기 (캐시화)
  - ListWindow를 열때 TodoStorage의 Records에 접근하는게 아닌 ListWindow 자체에 모든 Records를 불러오게끔 만들기 (아니면 기간을 선택해서 해당 기간의 Records만 불러오기)
 ### ② RoutineRecord를 수정할땐 시작 날짜, 종료 날짜 안보이게하고 Title, Content, 날짜만 보여주기
  - Schdule 창으로 적용시키거나 새로운 창 만들기

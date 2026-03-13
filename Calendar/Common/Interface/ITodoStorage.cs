/*
 * 데이터를 실제로 들고있는 객체가 구현해야하는 Interface
 * 
 * ITodoStorage를 구현하는 객체들은 아래 기능들을 구현해야한다
 * 1.저장소에 접근하여 데이터를 읽어와 들고있어야함
 * 2.현재 데이터 내에서 요청받은 데이터를 검색, 수정, 입력, 삭제하는 기능
 * 3.데이터의 수정, 삭제 등에의해 필요없어진 데이터으 정리
 */
namespace Calendar.Common.Interface
{
    public interface ITodoStorage
    {
    }
}

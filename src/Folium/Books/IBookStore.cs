namespace Folium.Books;

public interface IBookStore
{
    IReadOnlyList<Book> Read();

    void Replace(IReadOnlyList<Book> books);
}

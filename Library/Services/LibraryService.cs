using Library.Models;
using Microsoft.VisualBasic.FileIO;

namespace Library.Services;

public class LibraryService
{
    private readonly string BooksPath;
    private readonly string UsersPath;
    private readonly List<Book> books = new();
    private readonly List<User> users = new();
    private readonly Dictionary<User, List<Book>> borrowedBooks = new();
    private readonly object _lock = new();

    public LibraryService() : this("./Data/Books.csv", "./Data/Users.csv")
    {
    }

    public LibraryService(string booksPath, string usersPath)
    {
        BooksPath = booksPath;
        UsersPath = usersPath;
        ReadBooks();
        ReadUsers();
    }

    public List<User> GetUsers() => users;
    public Book? GetBook(int id) => books.FirstOrDefault(book => book.Id == id);
    public User? GetUser(int id) => users.FirstOrDefault(user => user.Id == id);

    public Dictionary<Book, int> GetBooks()
    {
        lock (_lock)
        {
            return books.GroupBy(book => book.ISBN)
                .ToDictionary(group => group.First(), group => group.Count(book => !IsBorrowed(book)));
        }
    }

    public void AddBook(Book book)
    {
        lock (_lock)
        {
            book.Id = books.Count == 0 ? 1 : books.Max(b => b.Id) + 1;
            books.Add(book);
            WriteBooks();
        }
    }

    public void EditBook(Book updated)
    {
        lock (_lock)
        {
            foreach (var book in books.Where(b => b.Id == updated.Id))
            {
                book.Title = updated.Title;
                book.Author = updated.Author;
                book.ISBN = updated.ISBN;
            }
            WriteBooks();
        }
    }

    public int CountCopies(string isbn) => books.Count(b => b.ISBN == isbn);

    public void DeleteAllCopies(string isbn)
    {
        lock (_lock)
        {
            foreach (var loans in borrowedBooks.Values)
                loans.RemoveAll(b => b.ISBN == isbn);
            books.RemoveAll(b => b.ISBN == isbn);
            WriteBooks();
        }
    }

    public void DeleteBook(int id)
    {
        lock (_lock)
        {
            var book = books.FirstOrDefault(b => b.Id == id);
            if (book != null)
            {
                foreach (var loans in borrowedBooks.Values) loans.Remove(book);
                books.Remove(book);
            }
            WriteBooks();
        }
    }

    public void AddUser(User user)
    {
        lock (_lock)
        {
            user.Id = users.Count == 0 ? 1 : users.Max(u => u.Id) + 1;
            users.Add(user);
            WriteUsers();
        }
    }

    public void EditUser(User updated)
    {
        lock (_lock)
        {
            var user = users.FirstOrDefault(u => u.Id == updated.Id);
            if (user == null) return;
            user.Name = updated.Name;
            user.Email = updated.Email;
            WriteUsers();
        }
    }

    public void DeleteUser(int id)
    {
        lock (_lock)
        {
            var user = users.FirstOrDefault(u => u.Id == id);
            if (user != null) borrowedBooks.Remove(user);
            users.RemoveAll(u => u.Id == id);
            WriteUsers();
        }
    }

    private bool IsBorrowed(Book book) => borrowedBooks.Values.Any(loans => loans.Contains(book));

    public Dictionary<User, List<Book>> GetBorrowedBooks()
    {
        lock (_lock)
        {
            return borrowedBooks.ToDictionary(pair => pair.Key, pair => pair.Value.ToList());
        }
    }

    public List<Book> GetBorrowedBooks(User user)
    {
        lock (_lock)
        {
            return borrowedBooks.TryGetValue(user, out var loans) ? loans.ToList() : new();
        }
    }

    public int CountAvailableCopies(string isbn)
    {
        lock (_lock)
        {
            return books.Count(book => book.ISBN == isbn && !IsBorrowed(book));
        }
    }

    public void BorrowBook(int userId, string isbn)
    {
        lock (_lock)
        {
            var user = users.FirstOrDefault(u => u.Id == userId);
            var book = books.FirstOrDefault(b => b.ISBN == isbn && !IsBorrowed(b));
            if (user == null || book == null) return;

            if (!borrowedBooks.ContainsKey(user)) borrowedBooks[user] = new();
            borrowedBooks[user].Add(book);
        }
    }

    public void ReturnBook(int userId, int bookId)
    {
        lock (_lock)
        {
            var user = users.FirstOrDefault(u => u.Id == userId);
            if (user == null || !borrowedBooks.TryGetValue(user, out var loans)) return;
            var book = loans.FirstOrDefault(b => b.Id == bookId);
            if (book != null) loans.Remove(book);
        }
    }

    private void WriteBooks()
    {
        var lines = books.Select(b => string.Join(",",
            b.Id.ToString(), CsvField(b.Title), CsvField(b.Author), CsvField(b.ISBN)));
        File.WriteAllLines(BooksPath, lines);
    }

    private void WriteUsers()
    {
        var lines = users.Select(u => string.Join(",",
            u.Id.ToString(), CsvField(u.Name), CsvField(u.Email)));
        File.WriteAllLines(UsersPath, lines);
    }

    private static string CsvField(string value) => "\"" + value.Replace("\"", "\"\"") + "\"";

    private void ReadBooks()
    {
        try
        {
            using var parser = new TextFieldParser(BooksPath);
            parser.SetDelimiters(",");
            parser.HasFieldsEnclosedInQuotes = true;

            while (!parser.EndOfData)
            {
                var fields = parser.ReadFields();
                if (fields == null || fields.Length < 4) continue;

                books.Add(new Book
                {
                    Id = int.Parse(fields[0].Trim()),
                    Title = fields[1].Trim(),
                    Author = fields[2].Trim(),
                    ISBN = fields[3].Trim()
                });
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred: {ex.Message}");
        }
    }

    private void ReadUsers()
    {
        try
        {
            using var parser = new TextFieldParser(UsersPath);
            parser.SetDelimiters(",");
            parser.HasFieldsEnclosedInQuotes = true;

            while (!parser.EndOfData)
            {
                var fields = parser.ReadFields();
                if (fields == null || fields.Length < 3) continue;

                users.Add(new User
                {
                    Id = int.Parse(fields[0].Trim()),
                    Name = fields[1].Trim(),
                    Email = fields[2].Trim()
                });
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred: {ex.Message}");
        }
    }
}

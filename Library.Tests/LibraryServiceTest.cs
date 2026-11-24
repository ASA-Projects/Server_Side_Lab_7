using Library.Models;
using Library.Services;

namespace Library.Tests;

[TestClass]
public sealed class LibraryServiceTest
{
    private string tempDirectory = null!;
    private string booksPath = null!;
    private string usersPath = null!;
    private LibraryService service = null!;

    [TestInitialize]
    public void Setup()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "LibraryTests-" + Guid.NewGuid());
        var dataDirectory = Path.Combine(tempDirectory, "Data");
        Directory.CreateDirectory(dataDirectory);
        booksPath = Path.Combine(dataDirectory, "Books.csv");
        usersPath = Path.Combine(dataDirectory, "Users.csv");
        File.WriteAllLines(booksPath,
        [
            "1,The Hobbit,Tolkien,111",
            "2,The Hobbit,Tolkien,111",
            "3,Emma,Austen,222"
        ]);
        File.WriteAllLines(usersPath,
        [
            "1,Alice,alice@example.com",
            "2,Bob,bob@example.com"
        ]);
        service = new LibraryService(booksPath, usersPath);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(tempDirectory)) Directory.Delete(tempDirectory, true);
    }

    [TestMethod]
    public void GetBook_ExistingId_ReturnsThatBook()
    {
        var book = service.GetBook(3);

        Assert.IsNotNull(book);
        Assert.AreEqual("Emma", book.Title);
        Assert.AreEqual("Austen", book.Author);
        Assert.AreEqual("222", book.ISBN);
    }

    [TestMethod]
    [DataRow(-1)]
    [DataRow(0)]
    [DataRow(999)]
    public void GetBook_UnknownId_ReturnsNull(int id)
    {
        var book = service.GetBook(id);

        Assert.IsNull(book);
    }

    [TestMethod]
    public void GetUser_ExistingId_ReturnsThatUser()
    {
        var user = service.GetUser(1);

        Assert.IsNotNull(user);
        Assert.AreEqual("Alice", user.Name);
        Assert.AreEqual("alice@example.com", user.Email);
    }

    [TestMethod]
    [DataRow(-1)]
    [DataRow(0)]
    [DataRow(999)]
    public void GetUser_UnknownId_ReturnsNull(int id)
    {
        var user = service.GetUser(id);

        Assert.IsNull(user);
    }

    [TestMethod]
    public void GetUsers_LoadedData_ReturnsAllUsers()
    {
        var users = service.GetUsers();

        Assert.HasCount(2, users);
    }

    [TestMethod]
    [DataRow("111", 2)]
    [DataRow("222", 1)]
    [DataRow("333", 0)]
    [DataRow("not-an-isbn", 0)]
    [DataRow("", 0)]
    public void CountCopies_ByIsbn_ReturnsNumberOfCopies(string isbn, int expected)
    {
        var count = service.CountCopies(isbn);

        Assert.AreEqual(expected, count);
    }

    [TestMethod]
    public void GetBooks_DuplicateIsbn_GroupsCopiesTogether()
    {
        var groups = service.GetBooks();

        Assert.HasCount(2, groups);
        Assert.AreEqual(2, groups.Single(pair => pair.Key.ISBN == "111").Value);
        Assert.AreEqual(1, groups.Single(pair => pair.Key.ISBN == "222").Value);
    }

    [TestMethod]
    public void Constructor_EmptyCsvFiles_LoadsEmptyLists()
    {
        File.WriteAllText(booksPath, "");
        File.WriteAllText(usersPath, "");

        var loaded = new LibraryService(booksPath, usersPath);

        Assert.HasCount(0, loaded.GetBooks());
        Assert.HasCount(0, loaded.GetUsers());
    }

    [TestMethod]
    public void Constructor_MissingCsvFiles_LoadsEmptyListsWithoutThrowing()
    {
        File.Delete(booksPath);
        File.Delete(usersPath);

        var loaded = new LibraryService(booksPath, usersPath);

        Assert.HasCount(0, loaded.GetBooks());
        Assert.HasCount(0, loaded.GetUsers());
    }

    [TestMethod]
    public void Constructor_ShortCsvRows_SkipsRowsAndLoadsValidData()
    {
        File.WriteAllText(booksPath, "incomplete,row\n1,Emma,Austen,222\n");
        File.WriteAllText(usersPath, "incomplete,row\n1,Alice,alice@example.com\n");

        var loaded = new LibraryService(booksPath, usersPath);

        Assert.HasCount(1, loaded.GetBooks());
        Assert.HasCount(1, loaded.GetUsers());
        Assert.AreEqual("Emma", loaded.GetBook(1)!.Title);
        Assert.AreEqual("Alice", loaded.GetUser(1)!.Name);
    }

    [TestMethod]
    public void Constructor_InvalidNumericIds_LoadsEmptyListsWithoutThrowing()
    {
        File.WriteAllText(booksPath, "invalid,Emma,Austen,222\n");
        File.WriteAllText(usersPath, "invalid,Alice,alice@example.com\n");

        var loaded = new LibraryService(booksPath, usersPath);

        Assert.HasCount(0, loaded.GetBooks());
        Assert.HasCount(0, loaded.GetUsers());
    }

    [TestMethod]
    public void AddBook_NewBook_AssignsNextIdAndSavesToCsv()
    {
        var book = new Book { Title = "Hamlet", Author = "Shakespeare", ISBN = "333" };

        service.AddBook(book);

        Assert.AreEqual(4, book.Id);
        Assert.AreSame(book, service.GetBook(4));
        StringAssert.Contains(File.ReadAllText(booksPath), "4,\"Hamlet\",\"Shakespeare\",\"333\"");
    }

    [TestMethod]
    public void AddBook_EmptyList_AssignsIdOne()
    {
        service.DeleteAllCopies("111");
        service.DeleteAllCopies("222");
        var book = new Book { Title = "Hamlet", Author = "Shakespeare", ISBN = "333" };

        service.AddBook(book);

        Assert.AreEqual(1, book.Id);
        Assert.AreSame(book, service.GetBook(1));
    }

    [TestMethod]
    public void AddBook_ExistingIsbn_IncreasesCopyCount()
    {
        var book = new Book { Title = "The Hobbit", Author = "Tolkien", ISBN = "111" };

        service.AddBook(book);

        Assert.AreEqual(3, service.CountCopies("111"));
        Assert.HasCount(2, service.GetBooks());
    }

    [TestMethod]
    public void AddBook_CommasAndQuotes_PreservesFieldsAfterReload()
    {
        var book = new Book { Title = "A, \"quoted\" title", Author = "Writer, Jr.", ISBN = "333" };

        service.AddBook(book);
        var loaded = new LibraryService(booksPath, usersPath);

        Assert.AreEqual(book.Title, loaded.GetBook(book.Id)!.Title);
        Assert.AreEqual(book.Author, loaded.GetBook(book.Id)!.Author);
    }

    [TestMethod]
    public void EditBook_ExistingId_UpdatesRecordAndSavesToCsv()
    {
        var updated = new Book { Id = 3, Title = "Updated", Author = "New Author", ISBN = "444" };

        service.EditBook(updated);
        var loaded = new LibraryService(booksPath, usersPath);

        Assert.AreEqual("Updated", service.GetBook(3)!.Title);
        Assert.AreEqual("New Author", loaded.GetBook(3)!.Author);
        Assert.AreEqual("444", loaded.GetBook(3)!.ISBN);
        Assert.AreEqual("The Hobbit", loaded.GetBook(1)!.Title);
    }

    [TestMethod]
    public void EditBook_UnknownId_DoesNotChangeRecords()
    {
        var updated = new Book { Id = 999, Title = "Missing", Author = "Nobody", ISBN = "999" };

        service.EditBook(updated);
        var loaded = new LibraryService(booksPath, usersPath);

        Assert.IsNull(loaded.GetBook(999));
        Assert.AreEqual(2, loaded.CountCopies("111"));
        Assert.AreEqual("Emma", loaded.GetBook(3)!.Title);
    }

    [TestMethod]
    public void DeleteBook_ExistingId_RemovesOneCopyAndSavesToCsv()
    {
        service.DeleteBook(1);
        var loaded = new LibraryService(booksPath, usersPath);

        Assert.IsNull(service.GetBook(1));
        Assert.IsNull(loaded.GetBook(1));
        Assert.IsNotNull(loaded.GetBook(2));
        Assert.AreEqual(1, loaded.CountCopies("111"));
    }

    [TestMethod]
    public void DeleteBook_UnknownId_DoesNotRemoveRecords()
    {
        service.DeleteBook(999);
        var loaded = new LibraryService(booksPath, usersPath);

        Assert.AreEqual(2, loaded.CountCopies("111"));
        Assert.AreEqual(1, loaded.CountCopies("222"));
    }

    [TestMethod]
    public void DeleteAllCopies_ExistingIsbn_RemovesMatchingCopiesAndSavesToCsv()
    {
        service.DeleteAllCopies("111");
        var loaded = new LibraryService(booksPath, usersPath);

        Assert.AreEqual(0, loaded.CountCopies("111"));
        Assert.IsNotNull(loaded.GetBook(3));
    }

    [TestMethod]
    public void DeleteAllCopies_UnknownIsbn_DoesNotRemoveRecords()
    {
        service.DeleteAllCopies("missing");

        Assert.AreEqual(2, service.CountCopies("111"));
        Assert.AreEqual(1, service.CountCopies("222"));
    }

    [TestMethod]
    public void AddUser_NewUser_AssignsNextIdAndSavesToCsv()
    {
        var user = new User { Name = "Carol", Email = "carol@example.com" };

        service.AddUser(user);

        Assert.AreEqual(3, user.Id);
        Assert.AreSame(user, service.GetUser(3));
        StringAssert.Contains(File.ReadAllText(usersPath), "3,\"Carol\",\"carol@example.com\"");
    }

    [TestMethod]
    public void AddUser_EmptyList_AssignsIdOne()
    {
        service.DeleteUser(1);
        service.DeleteUser(2);
        var user = new User { Name = "Carol", Email = "carol@example.com" };

        service.AddUser(user);

        Assert.AreEqual(1, user.Id);
        Assert.AreSame(user, service.GetUser(1));
    }

    [TestMethod]
    public void AddUser_CommasAndQuotes_PreservesNameAfterReload()
    {
        var user = new User { Name = "Carol, \"C\"", Email = "carol@example.com" };

        service.AddUser(user);
        var loaded = new LibraryService(booksPath, usersPath);

        Assert.AreEqual(user.Name, loaded.GetUser(user.Id)!.Name);
    }

    [TestMethod]
    public void EditUser_ExistingId_UpdatesRecordAndSavesToCsv()
    {
        var updated = new User { Id = 1, Name = "Changed", Email = "changed@example.com" };

        service.EditUser(updated);
        var loaded = new LibraryService(booksPath, usersPath);

        Assert.AreEqual("Changed", service.GetUser(1)!.Name);
        Assert.AreEqual("changed@example.com", loaded.GetUser(1)!.Email);
        Assert.AreEqual("Bob", loaded.GetUser(2)!.Name);
    }

    [TestMethod]
    public void EditUser_UnknownId_DoesNotChangeRecords()
    {
        var originalCsv = File.ReadAllText(usersPath);
        var updated = new User { Id = 999, Name = "Missing", Email = "missing@example.com" };

        service.EditUser(updated);

        Assert.IsNull(service.GetUser(999));
        Assert.AreEqual(originalCsv, File.ReadAllText(usersPath));
    }

    [TestMethod]
    public void DeleteUser_ExistingId_RemovesUserAndSavesToCsv()
    {
        service.DeleteUser(1);
        var loaded = new LibraryService(booksPath, usersPath);

        Assert.IsNull(service.GetUser(1));
        Assert.IsNull(loaded.GetUser(1));
        Assert.IsNotNull(loaded.GetUser(2));
    }

    [TestMethod]
    public void DeleteUser_UnknownId_DoesNotRemoveUsers()
    {
        service.DeleteUser(999);
        var loaded = new LibraryService(booksPath, usersPath);

        Assert.HasCount(2, loaded.GetUsers());
    }

    [TestMethod]
    public void BorrowBook_OneCopy_ReducesAvailableCopiesAndRecordsLoan()
    {
        var originalCsv = File.ReadAllText(booksPath);

        service.BorrowBook(1, "111");

        Assert.AreEqual(1, service.CountAvailableCopies("111"));
        Assert.AreEqual(2, service.CountCopies("111"));
        Assert.HasCount(1, service.GetBorrowedBooks(service.GetUser(1)!));
        Assert.HasCount(1, service.GetBorrowedBooks());
        Assert.AreEqual(originalCsv, File.ReadAllText(booksPath));
    }

    [TestMethod]
    public void BorrowBook_UnknownUser_DoesNothing()
    {
        service.BorrowBook(999, "111");

        Assert.AreEqual(2, service.CountAvailableCopies("111"));
        Assert.HasCount(0, service.GetBorrowedBooks());
    }

    [TestMethod]
    [DataRow("333")]
    [DataRow("")]
    public void BorrowBook_UnknownIsbn_DoesNothing(string isbn)
    {
        service.BorrowBook(1, isbn);

        Assert.AreEqual(2, service.CountAvailableCopies("111"));
        Assert.HasCount(0, service.GetBorrowedBooks());
    }

    [TestMethod]
    public void BorrowBook_NoCopiesAvailable_DoesNotCreateAnotherLoan()
    {
        service.BorrowBook(1, "222");

        service.BorrowBook(2, "222");

        Assert.AreEqual(0, service.CountAvailableCopies("222"));
        Assert.HasCount(1, service.GetBorrowedBooks(service.GetUser(1)!));
        Assert.HasCount(0, service.GetBorrowedBooks(service.GetUser(2)!));
    }

    [TestMethod]
    public void GetBooks_AllCopiesBorrowed_KeepsGroupWithZeroAvailable()
    {
        service.BorrowBook(1, "111");
        service.BorrowBook(2, "111");

        var groups = service.GetBooks();

        Assert.HasCount(2, groups);
        Assert.AreEqual(0, groups.Single(pair => pair.Key.ISBN == "111").Value);
    }

    [TestMethod]
    [DataRow("111", 2)]
    [DataRow("222", 1)]
    [DataRow("missing", 0)]
    public void CountAvailableCopies_NoLoans_ReturnsTotalCopies(string isbn, int expected)
    {
        var available = service.CountAvailableCopies(isbn);

        Assert.AreEqual(expected, available);
    }

    [TestMethod]
    public void GetBorrowedBooks_UserWithoutLoans_ReturnsEmptyList()
    {
        var user = service.GetUser(1)!;

        var loans = service.GetBorrowedBooks(user);

        Assert.HasCount(0, loans);
    }

    [TestMethod]
    public void ReturnBook_BorrowedCopy_RestoresAvailabilityAndRemovesLoan()
    {
        service.BorrowBook(1, "222");

        service.ReturnBook(1, 3);

        Assert.AreEqual(1, service.CountAvailableCopies("222"));
        Assert.HasCount(0, service.GetBorrowedBooks(service.GetUser(1)!));
        Assert.AreEqual(1, service.CountCopies("222"));
    }

    [TestMethod]
    public void ReturnBook_TwoBorrowedCopies_ReturnsOnlySelectedCopy()
    {
        service.BorrowBook(1, "111");
        service.BorrowBook(1, "222");

        service.ReturnBook(1, 3);

        Assert.AreEqual(1, service.CountAvailableCopies("111"));
        Assert.AreEqual(1, service.CountAvailableCopies("222"));
        Assert.HasCount(1, service.GetBorrowedBooks(service.GetUser(1)!));
        Assert.AreEqual("111", service.GetBorrowedBooks(service.GetUser(1)!)[0].ISBN);
    }

    [TestMethod]
    [DataRow(999, 3)]
    [DataRow(2, 3)]
    [DataRow(1, 999)]
    public void ReturnBook_InvalidUserOrBook_DoesNotChangeLoan(int userId, int bookId)
    {
        service.BorrowBook(1, "222");

        service.ReturnBook(userId, bookId);

        Assert.AreEqual(0, service.CountAvailableCopies("222"));
        Assert.HasCount(1, service.GetBorrowedBooks(service.GetUser(1)!));
    }

    [TestMethod]
    public void DeleteBook_BorrowedCopy_RemovesRelatedLoan()
    {
        service.BorrowBook(1, "222");

        service.DeleteBook(3);

        Assert.IsNull(service.GetBook(3));
        Assert.HasCount(0, service.GetBorrowedBooks(service.GetUser(1)!));
    }

    [TestMethod]
    public void DeleteAllCopies_BorrowedCopies_RemovesRelatedLoansOnly()
    {
        service.BorrowBook(1, "111");
        service.BorrowBook(2, "111");
        service.BorrowBook(1, "222");

        service.DeleteAllCopies("111");

        Assert.AreEqual(0, service.CountCopies("111"));
        Assert.HasCount(1, service.GetBorrowedBooks(service.GetUser(1)!));
        Assert.HasCount(0, service.GetBorrowedBooks(service.GetUser(2)!));
        Assert.AreEqual("222", service.GetBorrowedBooks(service.GetUser(1)!)[0].ISBN);
    }

    [TestMethod]
    public void DeleteUser_Borrower_ReleasesCopiesAndRemovesLoanEntry()
    {
        service.BorrowBook(1, "111");
        service.BorrowBook(2, "222");

        service.DeleteUser(1);

        Assert.AreEqual(2, service.CountAvailableCopies("111"));
        Assert.HasCount(1, service.GetBorrowedBooks());
        Assert.HasCount(1, service.GetBorrowedBooks(service.GetUser(2)!));
    }

    [TestMethod]
    public void EditBook_BorrowedCopy_UpdatesLoanDetails()
    {
        service.BorrowBook(1, "222");
        var updated = new Book { Id = 3, Title = "New Title", Author = "New Author", ISBN = "444" };

        service.EditBook(updated);

        Assert.AreEqual("New Title", service.GetBorrowedBooks(service.GetUser(1)!)[0].Title);
        Assert.AreEqual(0, service.CountAvailableCopies("444"));
    }

    [TestMethod]
    public void EditUser_Borrower_PreservesLoans()
    {
        service.BorrowBook(1, "222");
        var updated = new User { Id = 1, Name = "Changed", Email = "changed@example.com" };

        service.EditUser(updated);

        Assert.AreEqual("Changed", service.GetUser(1)!.Name);
        Assert.HasCount(1, service.GetBorrowedBooks(service.GetUser(1)!));
    }

    [TestMethod]
    public void Constructor_AfterBorrowing_ReloadsCatalogueWithoutLoans()
    {
        service.BorrowBook(1, "222");

        var loaded = new LibraryService(booksPath, usersPath);

        Assert.AreEqual(1, loaded.CountAvailableCopies("222"));
        Assert.HasCount(0, loaded.GetBorrowedBooks());
    }
}


The starter console program manages a library. It stores books and users in lists and tracks each user's borrowed books in a dictionary. ReadBooks() and ReadUsers() load the initial data from CSV files.

The program allows adding, editing, deleting, and listing books and users. Borrowing moves a book from the available list into a user's borrowed list. Returning moves it back. The console program does not save them to the CSV files as they stay in memory

The Blazor app needs to reproduce these features using pages, forms, and buttons instead of console menus and typed input. LibraryService handles the data operations and saves book and user changes to CSV files. Borrowed books are listed by user, with an option to select which books to return.

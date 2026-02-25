using Avalonia.Controls;
using Avalonia.Interactivity;
using LibraryApp.Models;
using Microsoft.EntityFrameworkCore;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using System;
using System.Collections.Generic;
using System.Linq;

namespace LibraryApp
{
    public partial class MainWindow : Window
    {
        private LibraryContext _context;
        private List<Book> _allBooksCache; 

        public MainWindow()
        {
            InitializeComponent();
            _context = new LibraryContext();
            _allBooksCache = new List<Book>();
            _context.Database.EnsureCreated();
        }

        private void Window_Opened(object sender, EventArgs e)
        {
            LoadFilters();
            LoadData();
        }

        private void LoadData()
        {
            // Подгружаем книги и все связи
            _allBooksCache = _context.Books
                .Include(b => b.Authors)
                .Include(b => b.Genres)
                .ToList();

            ApplyFilters();
        }

        private void LoadFilters()
        {
            var authors = _context.Authors.ToList();
            authors.Insert(0, new Author { Id = 0, LastName = "Все авторы" });
            AuthorFilter.ItemsSource = authors;
            AuthorFilter.SelectedIndex = 0;

            var genres = _context.Genres.ToList();
            genres.Insert(0, new Genre { Id = 0, Name = "Все жанры" });
            GenreFilter.ItemsSource = genres;
            GenreFilter.SelectedIndex = 0;
        }

        private void ApplyFilters()
        {
            if (_allBooksCache == null) return;
            var query = _allBooksCache.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(SearchBox.Text))
            {
                string search = SearchBox.Text.ToLower();
                query = query.Where(b => b.Title.ToLower().Contains(search));
            }
            if (AuthorFilter.SelectedItem is Author selAuthor && selAuthor.Id != 0)
            {
                query = query.Where(b => b.Authors.Any(a => a.Id == selAuthor.Id));
            }
            if (GenreFilter.SelectedItem is Genre selGenre && selGenre.Id != 0)
            {
                query = query.Where(b => b.Genres.Any(g => g.Id == selGenre.Id));
            }
            BooksGrid.ItemsSource = query.ToList();
        }

        private void Filter_Changed(object sender, RoutedEventArgs e) => ApplyFilters();
        private void Filter_Changed(object sender, TextChangedEventArgs e) => ApplyFilters();

        private void Reset_Click(object sender, RoutedEventArgs e)
        {
            SearchBox.Text = "";
            AuthorFilter.SelectedIndex = 0;
            GenreFilter.SelectedIndex = 0;
            LoadData();
        }

        // ==========================================
        // ИСПРАВЛЕНИЕ ОШИБКИ ДОБАВЛЕНИЯ КНИГИ
        // ==========================================
        private async void AddBook_Click(object sender, RoutedEventArgs e)
        {
            var newBook = new Book();
            var dialog = new BookWindow(newBook);
            var result = await dialog.ShowDialog<bool>(this);

            if (result)
            {
                // ПРОБЛЕМА БЫЛА ТУТ: newBook содержит авторов из другого контекста (BookWindow).
                // РЕШЕНИЕ: Подменяем их на авторов из текущего контекста (_context) по ID.

                // 1. Собираем ID выбранных авторов
                var authorIds = newBook.Authors.Select(a => a.Id).ToList();
                newBook.Authors.Clear(); // Очищаем "чужой" список

                foreach (var id in authorIds)
                {
                    // Находим "родного" автора в этом контексте
                    var localAuthor = _context.Authors.Find(id);
                    if (localAuthor != null) newBook.Authors.Add(localAuthor);
                }

                // 2. Собираем ID выбранных жанров
                var genreIds = newBook.Genres.Select(g => g.Id).ToList();
                newBook.Genres.Clear();

                foreach (var id in genreIds)
                {
                    var localGenre = _context.Genres.Find(id);
                    if (localGenre != null) newBook.Genres.Add(localGenre);
                }

                // Теперь можно безопасно добавлять
                _context.Books.Add(newBook);
                _context.SaveChanges();
                LoadData();
            }
        }

        private async void EditBook_Click(object sender, RoutedEventArgs e)
        {
            if (BooksGrid.SelectedItem is Book selectedBook)
            {
                // При редактировании тоже нужно быть аккуратным
                var dialog = new BookWindow(selectedBook);
                var result = await dialog.ShowDialog<bool>(this);

                if (result)
                {
                    // Здесь тоже нужно обновить связи через текущий контекст
                    // Но так как selectedBook уже из _context, 
                    // нам нужно просто синхронизировать списки.
                    
                    // Самый простой способ обновить Many-to-Many в EF Core при редактировании:
                    // 1. Получаем ID из отредактированного объекта
                    var newAuthorIds = selectedBook.Authors.Select(a => a.Id).ToList();
                    var newGenreIds = selectedBook.Genres.Select(g => g.Id).ToList();

                    // 2. Загружаем книгу из БД заново с Include, чтобы убедиться, что отслеживаем актуальное
                    var dbBook = _context.Books
                        .Include(b => b.Authors)
                        .Include(b => b.Genres)
                        .FirstOrDefault(b => b.Id == selectedBook.Id);

                    if (dbBook != null)
                    {
                        // Обновляем простые поля
                        dbBook.Title = selectedBook.Title;
                        dbBook.PublishYear = selectedBook.PublishYear;
                        dbBook.ISBN = selectedBook.ISBN;
                        dbBook.QuantityInStock = selectedBook.QuantityInStock;

                        // Обновляем авторов
                        dbBook.Authors.Clear();
                        foreach (var id in newAuthorIds)
                        {
                            var a = _context.Authors.Find(id);
                            if (a != null) dbBook.Authors.Add(a);
                        }

                        // Обновляем жанры
                        dbBook.Genres.Clear();
                        foreach (var id in newGenreIds)
                        {
                            var g = _context.Genres.Find(id);
                            if (g != null) dbBook.Genres.Add(g);
                        }

                        _context.SaveChanges();
                        BooksGrid.ItemsSource = null;
                        LoadData();
                    }
                }
            }
        }

        private async void DeleteBook_Click(object sender, RoutedEventArgs e)
        {
            if (BooksGrid.SelectedItem is Book selectedBook)
            {
                var box = MessageBoxManager.GetMessageBoxStandard("Удаление", $"Удалить книгу '{selectedBook.Title}'?", ButtonEnum.YesNo);
                if (await box.ShowAsync() == ButtonResult.Yes)
                {
                    _context.Books.Remove(selectedBook);
                    _context.SaveChanges();
                    LoadData();
                }
            }
        }

        private async void ManageAuthors_Click(object sender, RoutedEventArgs e)
        {
            await new AuthorWindow().ShowDialog(this);
            LoadFilters(); LoadData(); 
        }

        private async void ManageGenres_Click(object sender, RoutedEventArgs e)
        {
            await new GenreWindow().ShowDialog(this);
            LoadFilters(); LoadData();
        }
    }
}
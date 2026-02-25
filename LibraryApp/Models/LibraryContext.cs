using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq; // <--- ВАЖНО: Добавлена эта строка для работы .Select()

namespace LibraryApp.Models
{
    // === СУЩНОСТИ ===
    public class Author
    {
        public int Id { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public DateTime BirthDate { get; set; }
        public string Country { get; set; } = string.Empty;

        public ICollection<Book> Books { get; set; } = new List<Book>();

        public string FullName => $"{FirstName} {LastName}";
    }

    public class Genre
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        public ICollection<Book> Books { get; set; } = new List<Book>();
    }

    public class Book
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public int PublishYear { get; set; }
        public string ISBN { get; set; } = string.Empty;
        public int QuantityInStock { get; set; }

        public ICollection<Author> Authors { get; set; } = new List<Author>();
        public ICollection<Genre> Genres { get; set; } = new List<Genre>();

        // Свойства для отображения в DataGrid (через запятую)
        // Теперь Select будет работать, так как мы добавили using System.Linq;
        public string AuthorsDisplay => string.Join(", ", Authors.Select(a => a.LastName));
        public string GenresDisplay => string.Join(", ", Genres.Select(g => g.Name));
    }

    // === КОНТЕКСТ БД ===
    public class LibraryContext : DbContext
    {
        public DbSet<Book> Books { get; set; }
        public DbSet<Author> Authors { get; set; }
        public DbSet<Genre> Genres { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite("Data Source=library.db");
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // 1. Конфигурация BOOK
            modelBuilder.Entity<Book>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
                entity.Property(e => e.ISBN).HasMaxLength(20);

                entity.HasMany(b => b.Authors).WithMany(a => a.Books);
                entity.HasMany(b => b.Genres).WithMany(g => g.Books);
            });

            // 2. Конфигурация AUTHOR
            modelBuilder.Entity<Author>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.FirstName).IsRequired().HasMaxLength(100);
                entity.Property(e => e.LastName).IsRequired().HasMaxLength(100);
            });

            // 3. Конфигурация GENRE
            modelBuilder.Entity<Genre>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(50);
            });

            // Seed Data
            modelBuilder.Entity<Genre>().HasData(
                new Genre { Id = 1, Name = "Фантастика", Description = "Космос и будущее" },
                new Genre { Id = 2, Name = "Роман", Description = "Про любовь" }
            );

            modelBuilder.Entity<Author>().HasData(
                new Author { Id = 1, FirstName = "Александр", LastName = "Пушкин", Country = "Россия", BirthDate = new DateTime(1799, 6, 6) }
            );
        }
    }
}
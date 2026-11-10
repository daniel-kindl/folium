using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using Folium.Books;
using Folium.Storage;

string directory = Path.Combine(Path.GetTempPath(), "folium-storage-benchmark-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(directory);
try
{
    string[] columns = ["books", "cover_bytes", "library_bytes", "median_save_ms", "managed_allocated_bytes", "cover_storage_bytes", "cover_files_rewritten"];
    Console.WriteLine(string.Join(',', columns));
    foreach (int count in new[] { 0, 100, 1000 })
    {
        foreach (int coverBytes in new[] { 32768, 131072 })
        {
            Book[] books = CreateBooks(count, coverBytes);
            FileBookStore store = new(Path.Combine(directory, "library.json"));
            store.Replace(books);
            string[] covers = Directory.Exists(Path.Combine(directory, "library.json.covers"))
                ? Directory.GetFiles(Path.Combine(directory, "library.json.covers")) : [];
            DateTime stamp = new(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            foreach (string cover in covers)
            {
                File.SetLastWriteTimeUtc(cover, stamp);
            }

            double[] timings = new double[5];
            long[] allocations = new long[5];
            for (int sample = 0; sample < timings.Length; sample++)
            {
                if (books.Length > 0)
                {
                    books[0] = books[0] with { Status = sample % 2 == 0 ? ReadingStatus.Reading : ReadingStatus.Finished };
                }

                long allocated = GC.GetTotalAllocatedBytes(precise: true);
                Stopwatch watch = Stopwatch.StartNew();
                store.Replace(books);
                watch.Stop();
                timings[sample] = watch.Elapsed.TotalMilliseconds;
                allocations[sample] = GC.GetTotalAllocatedBytes(precise: true) - allocated;
            }

            Array.Sort(timings);
            Array.Sort(allocations);
            long bytes = new FileInfo(Path.Combine(directory, "library.json")).Length;
            long coverStorage = covers.Sum(cover => new FileInfo(cover).Length);
            int rewritten = covers.Count(cover => File.GetLastWriteTimeUtc(cover) != stamp);
            Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"{count},{coverBytes},{bytes},{timings[2]:F3},{allocations[2]},{coverStorage},{rewritten}"));
            if (store.Read().Count != count)
            {
                throw new InvalidOperationException("The saved book count differs from the fixture count.");
            }
        }
    }
}
finally
{
    Directory.Delete(directory, recursive: true);
}

static Book[] CreateBooks(int count, int coverBytes)
{
    Book[] books = new Book[count];
    for (int index = 0; index < count; index++)
    {
        byte[] cover = new byte[coverBytes];
        for (int offset = 0; offset < cover.Length; offset += 32)
        {
            byte[] block = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(string.Create(CultureInfo.InvariantCulture, $"23:{index}:{offset}")));
            block.CopyTo(cover, offset);
        }

        byte[] identifier = SHA256.HashData(BitConverter.GetBytes(index))[..16];
        BookMetadata metadata = new("/works/OL1W", "/books/OL1M", "Publisher", "2026", "", "", "eng", "Description", 1, cover);
        books[index] = new Book(new Guid(identifier), "Fixture title", "Fixture author", ReadingStatus.ToRead, metadata);
    }

    return books;
}

using cashbook.Data;
using cashbook.Interfaces;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System.Linq.Expressions;
using System.Reflection;

namespace cashbook.Repositories
{
    public class Repository<T> : IRepository<T> where T : class
    {
        private readonly ApplicationDbContext _db;
        internal DbSet<T> dbSet;

        public Repository(ApplicationDbContext db)
        {
            _db = db;
            dbSet = _db.Set<T>();
        }

        public async Task CreateAsync(T entity)
        {
            await dbSet.AddAsync(entity);
            await SaveAsync();
        }

        public async Task<T?> GetAsync(Expression<Func<T, bool>>? filter = null, bool tracked = true)
        {
            IQueryable<T> query = dbSet;

            if (!tracked)
            {
                query = query.AsNoTracking();
            }

            if (filter != null)
            {
                query = query.Where(filter);
            }

            var result = await query.FirstOrDefaultAsync();

            // If no result is found, return null
            if (result == null)
            {
                return null;
            }

            // Loop through each property of the result and deserialize JSON string properties
            var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (var property in properties)
            {
                // Check if the property type is string and contains JSON data
                if (property.PropertyType == typeof(string))
                {
                    var value = property.GetValue(result) as string;
                    if (!string.IsNullOrEmpty(value) && (value.Trim().StartsWith("{") || value.Trim().StartsWith("[")))
                    {
                        // Try to deserialize JSON and set it back to the property
                        try
                        {
                            var deserializedValue = JsonConvert.DeserializeObject(value);
                            property.SetValue(result, deserializedValue?.ToString());
                        }
                        catch (JsonReaderException)
                        {
                            // Handle JSON deserialization error if necessary
                            continue;
                        }
                    }
                }
            }

            return result;
        }
        public async Task<List<T>> GetAllAsync(Expression<Func<T, bool>>? filter = null)
        {
            IQueryable<T> query = dbSet;

            if (filter != null)
            {
                query = query.Where(filter);
            }

            var results = await query.ToListAsync();

            // Loop through each result and deserialize JSON string properties
            foreach (var item in results)
            {
                // Get all properties of the item
                var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);

                foreach (var property in properties)
                {
                    // Check if the property type is string and contains JSON data
                    if (property.PropertyType == typeof(string))
                    {
                        var value = property.GetValue(item) as string;
                        if (!string.IsNullOrEmpty(value) && (value.Trim().StartsWith("{") || value.Trim().StartsWith("[")))
                        {
                            // Try to deserialize JSON and set it back to the property
                            try
                            {
                                var deserializedValue = JsonConvert.DeserializeObject(value);
                                property.SetValue(item, deserializedValue);
                            }
                            catch (JsonReaderException)
                            {
                                // Handle JSON deserialization error if necessary
                                continue;
                            }
                        }
                    }
                }
            }

            return results;
        }

        public async Task RemoveAsync(T entity)
        {
            dbSet.Remove(entity);
            await SaveAsync();
        }

        public async Task<string?> GetUserRoleAsync(Guid userId, Guid businessId)
        {
            return await _db.BusinessUsers
                .Where(bu => bu.UserId == userId && bu.BusinessId == businessId)
                .Select(bu => bu.Role.ToLower())
                .FirstOrDefaultAsync();
        }



        public async Task SaveAsync()
        {
            await _db.SaveChangesAsync();
        }

        public async Task UpdateAsync(T entity)
        {
            dbSet.Update(entity);
            await SaveAsync();
        }

        public async Task<List<T>> GetPaginatedAsync(int? skip = null, int? take = null, Expression<Func<T, bool>>? filter = null)
        {
            int currentPage = skip ?? 1;
            int currentSize = take ?? 25;


            IQueryable<T> query = dbSet;


            if (filter != null)
            {
                query = query.Where(filter);
            }

            query = query.Skip((currentPage - 1) * currentSize)
                         .Take(currentSize);

            return await query.ToListAsync();
        }

        public async Task<int> GetCountAsync(Expression<Func<T, bool>>? filter = null)
        {
            IQueryable<T> query = dbSet;

            if (filter != null)
            {
                query = query.Where(filter);
            }

            return await query.CountAsync();
        }


        public async Task<List<string>> UploadFilesAsync(IEnumerable<IFormFile> files, string uploadPath)
        {
            if (files == null || !files.Any())
            {
                throw new ArgumentException("No files provided for upload.");
            }

            List<string> fileNames = new List<string>();

            try
            {
                foreach (var file in files)
                {
                    if (file.Length > 0)
                    {
                        // Ensure the upload path
                        // s, if not, create it
                        if (!Directory.Exists(uploadPath))
                        {
                            Directory.CreateDirectory(uploadPath);
                        }

                        // Generate a unique filename for each file to avoid conflicts
                        string uniqueFileName = Guid.NewGuid().ToString() + Path.GetExtension(file.FileName);

                        // Combine the path with the file name
                        string filePath = Path.Combine(uploadPath, uniqueFileName);

                        // Save the file to the specified path
                        using (var stream = new FileStream(filePath, FileMode.Create))
                        {
                            await file.CopyToAsync(stream);
                        }

                        // Add the unique file name to the list (to save in DB later)
                        fileNames.Add(uniqueFileName);
                    }
                }
            }
            catch (Exception ex)
            {
                // Remove any files that were uploaded before the error occurred
                foreach (var fileName in fileNames)
                {
                    string filePath = Path.Combine(uploadPath, fileName);
                    if (File.Exists(filePath))
                    {
                        File.Delete(filePath);
                    }
                }
                throw new InvalidOperationException("Error occurred while uploading files", ex);
            }

            // Return the file names as a comma-separated string
            return fileNames;
        }



    }
}

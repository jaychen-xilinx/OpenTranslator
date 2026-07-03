using System.Collections.Concurrent;
using OpenTranslator.Helpers;
using OpenTranslator.Models;
using SQLite;

namespace OpenTranslator.Services;

/// <summary>
/// 翻译历史服务接口
/// </summary>
public interface ITranslationHistoryService
{
    /// <summary>添加历史记录</summary>
    Task<int> AddAsync(TranslationHistoryItem item);

    /// <summary>搜索历史记录</summary>
    Task<List<TranslationHistoryItem>> SearchAsync(string? keyword, int offset = 0, int limit = 20);

    /// <summary>获取所有收藏的历史记录</summary>
    Task<List<TranslationHistoryItem>> GetFavoritesAsync(int offset = 0, int limit = 20);

    /// <summary>删除历史记录</summary>
    Task<int> DeleteAsync(int id);

    /// <summary>清空所有历史记录</summary>
    Task<int> ClearAllAsync();

    /// <summary>切换收藏状态</summary>
    Task<int> ToggleFavoriteAsync(int id);

    /// <summary>获取历史记录总数</summary>
    Task<int> GetTotalCountAsync(string? keyword = null);

    /// <summary>初始化数据库</summary>
    Task InitializeAsync();
}

/// <summary>
/// 翻译历史服务 - 使用 SQLite 存储翻译历史
/// </summary>
public class TranslationHistoryService : ITranslationHistoryService
{
    private readonly string _dbPath;
    private readonly SQLiteAsyncConnection _db;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private bool _initialized;

    public TranslationHistoryService(string? dbPath = null)
    {
        _dbPath = dbPath ?? Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            Constants.DataFolder,
            "translation_history.db");

        _db = new SQLiteAsyncConnection(_dbPath);
    }

    /// <summary>
    /// 初始化数据库，创建表
    /// </summary>
    public async Task InitializeAsync()
    {
        if (_initialized) return;

        await _initLock.WaitAsync();
        try
        {
            if (_initialized) return;

            // 确保目录存在
            var dir = Path.GetDirectoryName(_dbPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            // 创建表
            await _db.CreateTableAsync<TranslationHistoryItem>();

            // 创建索引以加速搜索
            await _db.ExecuteAsync(@"
                CREATE INDEX IF NOT EXISTS idx_history_created 
                ON TranslationHistoryItem(CreatedAt DESC)");

            await _db.ExecuteAsync(@"
                CREATE INDEX IF NOT EXISTS idx_history_favorite 
                ON TranslationHistoryItem(IsFavorite)");

            _initialized = true;
        }
        finally
        {
            _initLock.Release();
        }
    }

    /// <summary>
    /// 添加历史记录
    /// </summary>
    public async Task<int> AddAsync(TranslationHistoryItem item)
    {
        await InitializeAsync();
        item.CreatedAt = DateTime.Now;
        return await _db.InsertAsync(item);
    }

    /// <summary>
    /// 搜索历史记录
    /// </summary>
    public async Task<List<TranslationHistoryItem>> SearchAsync(string? keyword, int offset = 0, int limit = 20)
    {
        await InitializeAsync();

        if (string.IsNullOrWhiteSpace(keyword))
        {
            return await _db.Table<TranslationHistoryItem>()
                .OrderByDescending(x => x.CreatedAt)
                .Skip(offset)
                .Take(limit)
                .ToListAsync();
        }

        var searchPattern = $"%{keyword}%";
        return await _db.Table<TranslationHistoryItem>()
            .Where(x => x.SourceText.Contains(keyword) || x.TranslatedText.Contains(keyword))
            .OrderByDescending(x => x.CreatedAt)
            .Skip(offset)
            .Take(limit)
            .ToListAsync();
    }

    /// <summary>
    /// 获取所有收藏的历史记录
    /// </summary>
    public async Task<List<TranslationHistoryItem>> GetFavoritesAsync(int offset = 0, int limit = 20)
    {
        await InitializeAsync();
        return await _db.Table<TranslationHistoryItem>()
            .Where(x => x.IsFavorite)
            .OrderByDescending(x => x.CreatedAt)
            .Skip(offset)
            .Take(limit)
            .ToListAsync();
    }

    /// <summary>
    /// 删除历史记录
    /// </summary>
    public async Task<int> DeleteAsync(int id)
    {
        await InitializeAsync();
        return await _db.DeleteAsync<TranslationHistoryItem>(id);
    }

    /// <summary>
    /// 清空所有历史记录
    /// </summary>
    public async Task<int> ClearAllAsync()
    {
        await InitializeAsync();
        return await _db.ExecuteAsync("DELETE FROM TranslationHistoryItem WHERE IsFavorite = 0");
    }

    /// <summary>
    /// 切换收藏状态
    /// </summary>
    public async Task<int> ToggleFavoriteAsync(int id)
    {
        await InitializeAsync();

        var item = await _db.FindAsync<TranslationHistoryItem>(id);
        if (item == null) return 0;

        item.IsFavorite = !item.IsFavorite;
        return await _db.UpdateAsync(item);
    }

    /// <summary>
    /// 获取历史记录总数
    /// </summary>
    public async Task<int> GetTotalCountAsync(string? keyword = null)
    {
        await InitializeAsync();

        if (string.IsNullOrWhiteSpace(keyword))
        {
            return await _db.Table<TranslationHistoryItem>().CountAsync();
        }

        return await _db.Table<TranslationHistoryItem>()
            .Where(x => x.SourceText.Contains(keyword) || x.TranslatedText.Contains(keyword))
            .CountAsync();
    }
}

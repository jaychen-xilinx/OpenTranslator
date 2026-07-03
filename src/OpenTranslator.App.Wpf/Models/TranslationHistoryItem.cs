using System.ComponentModel;
using System.Runtime.CompilerServices;
using SQLite;

namespace OpenTranslator.Models;

/// <summary>
/// 翻译历史记录项
/// </summary>
public class TranslationHistoryItem : INotifyPropertyChanged
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    /// <summary>源文本</summary>
    public string SourceText { get; set; } = string.Empty;

    /// <summary>翻译后文本</summary>
    public string TranslatedText { get; set; } = string.Empty;

    /// <summary>源语言代码</summary>
    public string SourceLanguage { get; set; } = string.Empty;

    /// <summary>目标语言代码</summary>
    public string TargetLanguage { get; set; } = string.Empty;

    /// <summary>使用的模型名称</summary>
    public string ModelName { get; set; } = string.Empty;

    private DateTime _createdAt = DateTime.Now;
    /// <summary>创建时间</summary>
    public DateTime CreatedAt
    {
        get => _createdAt;
        set
        {
            if (_createdAt != value)
            {
                _createdAt = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CreatedAtText));
            }
        }
    }

    /// <summary>创建时间显示文本（WinUI 3 不支持 Binding StringFormat）</summary>
    [Ignore]
    public string CreatedAtText => _createdAt.ToString("HH:mm");

    /// <summary>是否收藏</summary>
    private bool _isFavorite;
    public bool IsFavorite
    {
        get => _isFavorite;
        set
        {
            if (_isFavorite != value)
            {
                _isFavorite = value;
                OnPropertyChanged();
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    /// <summary>
    /// 从 TranslationResult 创建历史记录项
    /// </summary>
    public static TranslationHistoryItem FromResult(TranslationResult result)
    {
        return new TranslationHistoryItem
        {
            SourceText = result.SourceText,
            TranslatedText = result.TranslatedText,
            SourceLanguage = result.SourceLanguage,
            TargetLanguage = result.TargetLanguage,
            ModelName = result.ModelName,
            CreatedAt = DateTime.Now,
            IsFavorite = false
        };
    }
}

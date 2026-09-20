using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ginei
{
    public sealed class GineiListColumn<T>
    {
        public readonly string title;
        public readonly Func<T, string> text;
        public readonly Comparison<T> comparison;
        public readonly float width;

        public GineiListColumn(string title, Func<T, string> text, Comparison<T> comparison = null, float width = 140f)
        {
            this.title = title ?? "";
            this.text = text ?? (_ => "");
            this.comparison = comparison;
            this.width = Mathf.Max(40f, width);
        }
    }

    /// <summary>
    /// 戦略画面で共用する実行時UI Toolkitリスト。列ソート、選択、詳細、ページ送り、決定・撤回を一つに束ねる。
    /// 右クリックはどの子要素上でも撤回として扱う。
    /// </summary>
    public sealed class GineiList<T> : VisualElement
    {
        private readonly Label titleLabel;
        private readonly VisualElement header;
        private readonly ScrollView rows;
        private readonly Label detailLabel;
        private readonly Label pageLabel;
        private readonly Button previousButton;
        private readonly Button nextButton;
        private readonly Button confirmButton;
        private readonly Button cancelButton;
        private readonly List<GineiListColumn<T>> columns = new List<GineiListColumn<T>>();
        private readonly List<T> items = new List<T>();
        private readonly List<T> visibleItems = new List<T>();
        private Func<T, string> detailText;
        private int page;
        private int sortColumn = -1;
        private bool ascending = true;
        private T selected;
        private bool hasSelection;

        public int PageSize { get; set; } = 20;
        public int CurrentPage => page;
        public int PageCount => Mathf.Max(1, Mathf.CeilToInt(items.Count / (float)Mathf.Max(1, PageSize)));
        public IReadOnlyList<T> VisibleItems => visibleItems;
        public bool HasSelection => hasSelection;
        public T SelectedItem => selected;
        public event Action<T> SelectionChanged;
        public event Action<T> Confirmed;
        public event Action Cancelled;

        public GineiList(string title = "一覧")
        {
            style.flexGrow = 1f;
            style.minHeight = 180f;

            titleLabel = new Label(title) { name = "ginei-list-title" };
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.fontSize = 18f;
            Add(titleLabel);

            header = new VisualElement { name = "ginei-list-header" };
            header.style.flexDirection = FlexDirection.Row;
            Add(header);

            rows = new ScrollView(ScrollViewMode.Vertical) { name = "ginei-list-rows" };
            rows.style.flexGrow = 1f;
            Add(rows);

            detailLabel = new Label { name = "ginei-list-detail" };
            detailLabel.style.whiteSpace = WhiteSpace.Normal;
            detailLabel.style.minHeight = 42f;
            Add(detailLabel);

            var footer = new VisualElement { name = "ginei-list-footer" };
            footer.style.flexDirection = FlexDirection.Row;
            previousButton = new Button(PreviousPage) { text = "◀" };
            pageLabel = new Label();
            pageLabel.style.flexGrow = 1f;
            pageLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            nextButton = new Button(NextPage) { text = "▶" };
            confirmButton = new Button(ConfirmSelection) { text = "決定" };
            cancelButton = new Button(Cancel) { text = "撤回" };
            footer.Add(previousButton);
            footer.Add(pageLabel);
            footer.Add(nextButton);
            footer.Add(confirmButton);
            footer.Add(cancelButton);
            Add(footer);

            RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 1) return;
                Cancel();
                evt.StopPropagation();
            });
            Refresh();
        }

        public void SetTitle(string title) => titleLabel.text = title ?? "";

        public void SetColumns(IEnumerable<GineiListColumn<T>> newColumns)
        {
            columns.Clear();
            if (newColumns != null) columns.AddRange(newColumns);
            sortColumn = -1;
            ascending = true;
            RebuildHeader();
            Refresh();
        }

        public void SetItems(IEnumerable<T> newItems)
        {
            items.Clear();
            if (newItems != null) items.AddRange(newItems);
            page = 0;
            hasSelection = false;
            selected = default;
            ApplySort();
            Refresh();
        }

        public void SetDetailFormatter(Func<T, string> formatter)
        {
            detailText = formatter;
            RefreshDetail();
        }

        public void SortByColumn(int index)
        {
            if (index < 0 || index >= columns.Count || columns[index].comparison == null) return;
            if (sortColumn == index) ascending = !ascending;
            else { sortColumn = index; ascending = true; }
            ApplySort();
            page = 0;
            RebuildHeader();
            Refresh();
        }

        public void SelectVisibleRow(int index)
        {
            if (index < 0 || index >= visibleItems.Count) return;
            selected = visibleItems[index];
            hasSelection = true;
            SelectionChanged?.Invoke(selected);
            Refresh();
        }

        public void NextPage()
        {
            if (page + 1 < PageCount) { page++; Refresh(); }
        }

        public void PreviousPage()
        {
            if (page > 0) { page--; Refresh(); }
        }

        public void ConfirmSelection()
        {
            if (hasSelection) Confirmed?.Invoke(selected);
        }

        public void Cancel()
        {
            Cancelled?.Invoke();
        }

        private void ApplySort()
        {
            if (sortColumn < 0 || sortColumn >= columns.Count) return;
            Comparison<T> comparison = columns[sortColumn].comparison;
            if (comparison == null) return;
            int direction = ascending ? 1 : -1;
            items.Sort((a, b) => direction * comparison(a, b));
        }

        private void RebuildHeader()
        {
            header.Clear();
            for (int i = 0; i < columns.Count; i++)
            {
                int columnIndex = i;
                string marker = sortColumn == i ? (ascending ? " ▲" : " ▼") : "";
                var button = new Button(() => SortByColumn(columnIndex)) { text = columns[i].title + marker };
                button.style.width = columns[i].width;
                header.Add(button);
            }
        }

        private void Refresh()
        {
            int pageSize = Mathf.Max(1, PageSize);
            page = Mathf.Clamp(page, 0, PageCount - 1);
            int start = page * pageSize;
            int end = Mathf.Min(items.Count, start + pageSize);
            visibleItems.Clear();
            rows.Clear();
            for (int i = start; i < end; i++)
            {
                T item = items[i];
                int visibleIndex = visibleItems.Count;
                visibleItems.Add(item);
                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                for (int c = 0; c < columns.Count; c++)
                {
                    var cell = new Label(columns[c].text(item));
                    cell.style.width = columns[c].width;
                    row.Add(cell);
                }
                row.RegisterCallback<ClickEvent>(_ => SelectVisibleRow(visibleIndex));
                rows.Add(row);
            }
            pageLabel.text = $"{page + 1} / {PageCount}";
            previousButton.SetEnabled(page > 0);
            nextButton.SetEnabled(page + 1 < PageCount);
            confirmButton.SetEnabled(hasSelection);
            RefreshDetail();
        }

        private void RefreshDetail()
        {
            detailLabel.text = hasSelection && detailText != null ? detailText(selected) : "";
        }
    }
}

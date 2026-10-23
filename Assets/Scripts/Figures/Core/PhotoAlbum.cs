using System;
using System.Collections.Generic;

namespace Figures
{
    /// <summary>Снимок в альбоме: что попало в кадр и выложен ли он.</summary>
    public sealed class AlbumPhoto
    {
        internal AlbumPhoto(int id, CollectionShot shot)
        {
            Id = id;
            Shot = shot;
        }

        /// <summary>Номер снимка. Не переиспользуется: удалённый снимок не всплывёт под чужим номером.</summary>
        public int Id { get; }

        public CollectionShot Shot { get; }

        public bool IsPublished { get; internal set; }

        /// <summary>Что принесла публикация. Пусто, пока снимок не выложен.</summary>
        public PhotoPost Post { get; internal set; }
    }

    /// <summary>
    /// Карта памяти камеры: снимки ждут здесь, пока игрок не выберет, что выложить.
    /// Вместимость конечна — иначе снимать всё подряд ничего не стоит, и выбор лучшего
    /// кадра перестаёт быть решением.
    ///
    /// Альбом денег не знает: выкладывает <see cref="CollectionStudio"/>.
    /// </summary>
    public sealed class PhotoAlbum
    {
        private readonly List<AlbumPhoto> _photos = new();
        private int _nextId = 1;

        public PhotoAlbum(int capacity) => Capacity = Math.Max(1, capacity);

        /// <summary>Снимок добавили, удалили или выложили.</summary>
        public event Action Changed;

        /// <summary>Снимок удалён: виду пора освободить его картинку.</summary>
        public event Action<int> Removed;

        public int Capacity { get; }

        public IReadOnlyList<AlbumPhoto> Photos => _photos;

        public int Count => _photos.Count;

        public bool IsFull => _photos.Count >= Capacity;

        public bool TryTake(in CollectionShot shot, out AlbumPhoto photo)
        {
            if (IsFull)
            {
                photo = null;
                return false;
            }

            photo = new AlbumPhoto(_nextId++, shot);
            _photos.Add(photo);

            Changed?.Invoke();
            return true;
        }

        public bool Remove(int id)
        {
            var index = IndexOf(id);
            if (index < 0) return false;

            _photos.RemoveAt(index);

            Removed?.Invoke(id);
            Changed?.Invoke();
            return true;
        }

        public bool TryGet(int id, out AlbumPhoto photo)
        {
            var index = IndexOf(id);
            photo = index >= 0 ? _photos[index] : null;
            return photo != null;
        }

        internal void MarkPublished(AlbumPhoto photo, in PhotoPost post)
        {
            photo.IsPublished = true;
            photo.Post = post;
            Changed?.Invoke();
        }

        private int IndexOf(int id)
        {
            for (int i = 0; i < _photos.Count; i++)
                if (_photos[i].Id == id) return i;
            return -1;
        }
    }
}

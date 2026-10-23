using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Figures
{
    /// <summary>
    /// Картинки снимков альбома. Ядро хранит, что попало в кадр, а здесь — как кадр
    /// выглядел: текстура живёт в движке, и альбому о ней знать незачем.
    ///
    /// Картинка освобождается вместе со снимком: альбом сообщает об удалении, и
    /// текстура не висит в памяти до выхода из игры.
    /// </summary>
    public sealed class PhotoPrints : IDisposable
    {
        private const int DepthBits = 24;

        private readonly Dictionary<int, RenderTexture> _prints = new();
        private readonly PhotoAlbum _album;

        public PhotoPrints(PhotoAlbum album)
        {
            _album = album;
            if (_album != null) _album.Removed += Release;
        }

        /// <summary>
        /// Снять картинку для снимка: кадр камеры целиком, обрезанный по рамке. Интерфейс
        /// поверх экрана в неё не попадает — он рисуется не камерой.
        /// </summary>
        /// <param name="viewport">Рамка в долях экрана.</param>
        /// <param name="width">Ширина готовой картинки, пиксели.</param>
        public void Capture(int photoId, Camera camera, Rect viewport, int width)
        {
            if (camera == null || viewport.width <= 0f || viewport.height <= 0f) return;

            var aspect = Screen.height > 0 ? (float)Screen.width / Screen.height : camera.aspect;
            var fullWidth = Mathf.CeilToInt(width / viewport.width);
            var fullHeight = Mathf.CeilToInt(fullWidth / aspect);

            var full = RenderTexture.GetTemporary(fullWidth, fullHeight, DepthBits);
            try
            {
                Render(camera, full);

                var print = new RenderTexture(width, Mathf.CeilToInt(fullHeight * viewport.height), 0)
                {
                    name = $"Photo {photoId}"
                };

                Graphics.Blit(full, print, viewport.size, viewport.position);

                Release(photoId);
                _prints[photoId] = print;
            }
            finally
            {
                RenderTexture.ReleaseTemporary(full);
            }
        }

        public Texture Of(int photoId) => _prints.TryGetValue(photoId, out var print) ? print : null;

        public void Dispose()
        {
            if (_album != null) _album.Removed -= Release;

            foreach (var print in _prints.Values)
                if (print != null) UnityEngine.Object.Destroy(print);

            _prints.Clear();
        }

        /// <summary>
        /// Конвейер рисует в текстуру по запросу. Если запрос он не поддерживает, остаётся
        /// старый путь через targetTexture — камера при этом возвращается на экран.
        /// </summary>
        private static void Render(Camera camera, RenderTexture target)
        {
            var request = new RenderPipeline.StandardRequest { destination = target };
            if (RenderPipeline.SupportsRenderRequest(camera, request))
            {
                RenderPipeline.SubmitRenderRequest(camera, request);
                return;
            }

            var previous = camera.targetTexture;
            try
            {
                camera.targetTexture = target;
                camera.Render();
            }
            finally
            {
                camera.targetTexture = previous;
            }
        }

        private void Release(int photoId)
        {
            if (!_prints.Remove(photoId, out var print) || print == null) return;

            print.Release();
            UnityEngine.Object.Destroy(print);
        }
    }
}

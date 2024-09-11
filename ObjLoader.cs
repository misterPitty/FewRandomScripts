using Dummiesman;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace XSight.Providers
{
    public class ObjLoader
    {
        private Dictionary<ArObjectLoadInfo, GameObject> _gameObjectsCache = new();
        private Dictionary<string, string> _modelsCache = new();
        private Dictionary<string, string> _materialsCache = new();
        private Dictionary<string, Texture> _texturesCache = new();

        private List<ArObjectLoadInfo> _objectsInLoad = new();
        private List<string> _modelsInLoad = new();
        private List<string> _materialsInLoad = new();
        private List<string> _texturesInLoad = new();

        public void Load(string modelLink, string materialLink, string textureLink, Action<GameObject> successCallback, Action<string> failureCallback)
        {
            // Get from gameobjects cache if exist

            if (TryGetGameObject(modelLink, materialLink, textureLink, out GameObject go))
            {
                successCallback?.Invoke(go);
                return;
            }
            
            // Check if we have same object in load

            if (TryGetLoadInfo(modelLink, materialLink, textureLink, out ArObjectLoadInfo info))
            {
                info.TryAddSuccessCallback(successCallback);
                info.TryAddFailureCallback(failureCallback);
                return;
            }

            //  Add  object to loaded list

            var loadObject = new ArObjectLoadInfo(modelLink, materialLink, textureLink);
            loadObject.TryAddSuccessCallback(successCallback);
            loadObject.TryAddFailureCallback(failureCallback);
            _objectsInLoad.Add(loadObject);

            bool needToLoadSomething = false;

            if (!_modelsCache.ContainsKey(modelLink) || !_modelsInLoad.Contains(modelLink))
            {
                needToLoadSomething = true;
                LoadModel(modelLink);
            }

            if (!_materialsCache.ContainsKey(materialLink) || !_materialsInLoad.Contains(materialLink))
            {
                needToLoadSomething = true;
                LoadMaterial(materialLink);
            }

            if (!_texturesCache.ContainsKey(textureLink) || !_texturesInLoad.Contains(textureLink))
            {
                needToLoadSomething = true;
                LoadTexture(textureLink);
            }

            if (!needToLoadSomething)
            {
                CheckLoadedGameObjects();
            }
        }

        private bool TryGetGameObject(string modelLink, string materialLink, string textureLink, out GameObject result)
        {
            foreach (var kvp in _gameObjectsCache)
            {
                if (kvp.Key.Equals(modelLink, materialLink, textureLink))
                {
                    result = kvp.Value;
                    return true;
                }
            }

            result = null;
            return false;
        }

        private bool TryGetLoadInfo(string modelLink, string materialLink, string textureLink, out ArObjectLoadInfo result)
        {
            foreach (var loadObj in _objectsInLoad)
            {
                if (loadObj.Equals(modelLink, materialLink, textureLink))
                {
                    result = loadObj;
                    return true;
                }
            }

            result = null;
            return false;
        }

        private void LoadModel(string modelLink)
        {
            _modelsInLoad.Add(modelLink);

            SendAsyncGetWebRequest(modelLink, data =>
            {
                _modelsCache[modelLink] = data;

                if (_modelsInLoad.Contains(modelLink))
                {
                    _modelsInLoad.Remove(modelLink);
                }

                CheckLoadedGameObjects();
            });
        }

        private void LoadMaterial(string materialLink)
        {
            _materialsInLoad.Add(materialLink);

            SendAsyncGetWebRequest(materialLink, data =>
            {
                _materialsCache[materialLink] = data;

                if (_materialsInLoad.Contains(materialLink))
                {
                    _materialsInLoad.Remove(materialLink);
                }

                CheckLoadedGameObjects();
            });
        }

        private void LoadTexture(string textureLink)
        {
            _texturesInLoad.Add(textureLink);

            SendAsyncGetTextureWebRequest(textureLink,
            texture =>
            {
                _texturesCache[textureLink] = texture;

                if (_texturesInLoad.Contains(textureLink))
                {
                    _texturesInLoad.Remove(textureLink);
                }

                CheckLoadedGameObjects();
            },
            error =>
            {
                var loadingObjects = _objectsInLoad.FindAll(lo => lo.TextureLink == textureLink);
                loadingObjects.ForEach(obj => obj.CanIgnoreTexture = true);

                if (_texturesInLoad.Contains(textureLink))
                {
                    _texturesInLoad.Remove(textureLink);
                }

                CheckLoadedGameObjects();
            });
        }

        private void CheckLoadedGameObjects()
        {
            for (var i = 0; i < _objectsInLoad.Count; i++)
            {
                var loadObj = _objectsInLoad[i];

                if (!_modelsCache.ContainsKey(loadObj.ModelLink)
                    || !_materialsCache.ContainsKey(loadObj.MaterialLink)
                    || (!loadObj.CanIgnoreTexture && !_texturesCache.ContainsKey(loadObj.TextureLink)))
                {
                    continue;
                }

                _objectsInLoad.RemoveAt(i--);

                var model = new MemoryStream(Encoding.UTF8.GetBytes(_modelsCache[loadObj.ModelLink]));
                var material = new MemoryStream(Encoding.UTF8.GetBytes(_materialsCache[loadObj.MaterialLink]));

                var newGameObject = new OBJLoader().Load(model, material, _texturesCache[loadObj.TextureLink]);

                model.Close();
                material.Close();

                _gameObjectsCache[loadObj] = newGameObject;

                foreach (var succesCallback in loadObj.SuccessCallbacks)
                {
                    succesCallback?.Invoke(newGameObject);
                }
            }
        }

        private async void SendAsyncGetWebRequest(string url, Action<string> successCallback, Action<string> failureCallback = null, int attemptCount = 3)
        {
            using UnityWebRequest webRequest = UnityWebRequest.Get(url);
            var asyncOperation = webRequest.SendWebRequest();

            while (!asyncOperation.isDone)
            {
                await Task.Yield();
            }

            if (webRequest == null
                || webRequest.downloadHandler == null
                || webRequest.result != UnityWebRequest.Result.Success)
            {
                if (attemptCount > 0)
                {
                    SendAsyncGetWebRequest(url, successCallback, failureCallback, --attemptCount);
                }
                else
                {
                    Debug.Log($"GetWebRequest Error: {url}. {webRequest?.error}");
                    failureCallback?.Invoke(webRequest?.error);
                }
            }
            else
            {
                successCallback?.Invoke(webRequest.downloadHandler.text);
            }
        }

        private async void SendAsyncGetTextureWebRequest(string url, Action<Texture2D> successCallback, Action<string> failureCallback = null, int attemptCount = 3)
        {
            using UnityWebRequest webRequest = UnityWebRequestTexture.GetTexture(url);
            var asyncOperation = webRequest.SendWebRequest();

            while (!asyncOperation.isDone)
            {
                await Task.Yield();
            }

            if (webRequest == null
                || webRequest.downloadHandler == null
                || webRequest.result != UnityWebRequest.Result.Success)
            {
                if (attemptCount > 0)
                {
                    SendAsyncGetTextureWebRequest(url, successCallback, failureCallback, --attemptCount);
                }
                else
                {
                    Debug.Log($"GetWebRequest Error: {url}. {webRequest?.error}");
                    failureCallback?.Invoke(webRequest?.error);
                }
            }
            else
            {
                successCallback?.Invoke(((DownloadHandlerTexture)webRequest.downloadHandler).texture);
            }
        }
    }

    public class ArObjectLoadInfo
    {
        public string ModelLink { get; private set; }
        public string MaterialLink { get; private set; }
        public string TextureLink { get; private set; }
        public bool CanIgnoreTexture { get; set; } = false;
        public List<Action<GameObject>> SuccessCallbacks => _successCallbacks;
        public List<Action<string>> FailureCallbacks => _failureCallbacks;

        private List<Action<GameObject>> _successCallbacks = new();
        private List<Action<string>> _failureCallbacks = new();

        public ArObjectLoadInfo(string modelLink, string materialLink, string textureLink)
        {
            ModelLink = modelLink;
            MaterialLink = materialLink;
            TextureLink = textureLink;
        }

        public void TryAddSuccessCallback(Action<GameObject> callback)
        {
            if (!_successCallbacks.Contains(callback))
            {
                _successCallbacks.Add(callback);
            }
        }

        public void TryAddFailureCallback(Action<string> callback)
        {
            if (!_failureCallbacks.Contains(callback))
            {
                _failureCallbacks.Add(callback);
            }
        }

        public bool Equals(string modelLink, string materialLink, string textureLink)
        {
            return ModelLink.Equals(modelLink) && MaterialLink.Equals(materialLink) && TextureLink.Equals(textureLink);
        }
    }
}
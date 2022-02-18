using UnityEngine;
using System.Collections.Generic;
namespace zFramework.Pool
{
    public sealed class ObjectPool : MonoBehaviour
    {
        #region Fileds
        public StartupPoolMode startupPoolMode;
        public StartupPool[] startupPools;
        static ObjectPool _instance;
        bool startupPoolsCreated;
        Dictionary<GameObject, GameObject> spawnedObjects = new Dictionary<GameObject, GameObject>();
        Dictionary<GameObject, Queue<GameObject>> pooledObjects = new Dictionary<GameObject, Queue<GameObject>>();
        #endregion

        #region Singleton and pre-instantiate
        public static ObjectPool Instance
        {
            get
            {
                if (!_instance)
                {
                    _instance = (ObjectPool)FindObjectOfType(typeof(ObjectPool));
                    if (!_instance)
                    {
                        var go = new GameObject("[ObjectPool]");
                        _instance = go.AddComponent<ObjectPool>();
                    }
                }
                return _instance;
            }
        }
        void Awake()
        {
            if (_instance != null && _instance.gameObject != gameObject)
            {
                if (Application.isPlaying)
                {
                    Destroy(gameObject);
                }
                else
                {
                    DestroyImmediate(gameObject);
                }
            }
            else
            {
                _instance = GetComponent<ObjectPool>();
                DontDestroyOnLoad(gameObject);
            }
            hideFlags = HideFlags.HideAndDontSave;
            if (startupPoolMode == StartupPoolMode.Awake)
                CreateStartupPools();
        }

        void Start()
        {
            if (startupPoolMode == StartupPoolMode.Start)
                CreateStartupPools();
        }

        public static void CreateStartupPools()
        {
            if (!Instance.startupPoolsCreated)
            {
                Instance.startupPoolsCreated = true;
                var pools = Instance.startupPools;
                if (pools != null && pools.Length > 0)
                    for (int i = 0; i < pools.Length; ++i)
                        CreatePool(pools[i].prefab, pools[i].size);
            }
        }
        #endregion

        #region Pool Create
        public static void CreatePool<T>(T prefab, int initialPoolSize) where T : Component => CreatePool(prefab.gameObject, initialPoolSize);
        public static void CreatePool(GameObject prefab, int initialPoolSize)
        {
            if (prefab != null && !Instance.pooledObjects.ContainsKey(prefab))
            {
                var list = new Queue<GameObject>();
                Instance.pooledObjects.Add(prefab, list);

                if (initialPoolSize > 0)
                {
                    bool active = prefab.activeSelf;
                    prefab.SetActive(false);
                    Transform parent = Instance.transform;
                    while (list.Count < initialPoolSize)
                    {
                        var obj = Instantiate(prefab);
                        obj.transform.SetParent(parent, !(obj.transform is RectTransform));
                        list.Enqueue(obj);
                    }
                    prefab.SetActive(active);
                }
            }
        }
        #endregion

        #region Spawn
        public static GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation) => Spawn(prefab, null, position, rotation);
        public static GameObject Spawn(GameObject prefab, Transform parent, Vector3 position) => Spawn(prefab, parent, position, Quaternion.identity);
        public static GameObject Spawn(GameObject prefab, Transform parent) => Spawn(prefab, parent, Vector3.zero, Quaternion.identity);
        public static GameObject Spawn(GameObject prefab, Vector3 position) => Spawn(prefab, null, position, Quaternion.identity);
        public static GameObject Spawn(GameObject prefab) => Spawn(prefab, null, Vector3.zero, Quaternion.identity);
        public static T Spawn<T>(T prefab, Transform parent, Vector3 position, Quaternion rotation) where T : Component => Spawn(prefab.gameObject, parent, position, rotation).GetComponent<T>();
        public static T Spawn<T>(T prefab, Vector3 position, Quaternion rotation) where T : Component => Spawn(prefab.gameObject, null, position, rotation).GetComponent<T>();
        public static T Spawn<T>(T prefab, Transform parent, Vector3 position) where T : Component => Spawn(prefab.gameObject, parent, position, Quaternion.identity).GetComponent<T>();
        public static T Spawn<T>(T prefab, Vector3 position) where T : Component => Spawn(prefab.gameObject, null, position, Quaternion.identity).GetComponent<T>();
        public static T Spawn<T>(T prefab, Transform parent) where T : Component => Spawn(prefab.gameObject, parent, Vector3.zero, Quaternion.identity).GetComponent<T>();
        public static T Spawn<T>(T prefab) where T : Component => Spawn(prefab.gameObject, null, Vector3.zero, Quaternion.identity).GetComponent<T>();
        public static GameObject Spawn(GameObject prefab, Transform parent, Vector3 position, Quaternion rotation)
        {
            GameObject obj = null;
            if (Instance.pooledObjects.TryGetValue(prefab, out Queue<GameObject> list))  //1. check pool first
            {
                while (obj == null && list.Count > 0) // in case of unexpected destroy 
                {
                    obj = list.Dequeue();
                }
            }
            if (!obj) obj = Instantiate(prefab); // 2. instantiate if not pooled
            obj.transform.SetParent(parent, !(obj.transform is RectTransform)); // 3. the second param should be false if the object is a ui component.
            obj.transform.localPosition = position;
            obj.transform.localRotation = rotation;
            if (Instance.pooledObjects.ContainsKey(prefab)) //4.only record spawned object if the prefab has been init before
            {
                Instance.spawnedObjects.Add(obj, prefab);
            }
            return obj;
        }
        #endregion

        #region Recyle
        public static void Recycle<T>(T obj) where T : Component => Recycle(obj.gameObject);
        public static void Recycle(GameObject obj)
        {
            if (Instance.spawnedObjects.TryGetValue(obj, out GameObject prefab))
                Recycle(obj, prefab);
            else
                DestroyImmediate(obj);
        }
        static void Recycle(GameObject obj, GameObject prefab)
        {
            Instance.pooledObjects[prefab].Enqueue(obj);
            Instance.spawnedObjects.Remove(obj);
            obj.transform.SetParent(Instance.transform, !(obj.transform is RectTransform));
            obj.SetActive(false);
        }

        public static void RecycleAll<T>(T prefab) where T : Component => RecycleAll(prefab.gameObject);
        public static void RecycleAll(GameObject prefab)
        {
            var temp = new Queue<GameObject>();
            foreach (var item in Instance.spawnedObjects)
            {
                if (item.Value == prefab)
                {
                    temp.Enqueue(item.Key);
                }
            }
            while (temp.Count > 0)
            {
                Recycle(temp.Dequeue());
            }
        }
        public static void RecycleAll()
        {
            var temp = new Queue<GameObject>(Instance.spawnedObjects.Keys);
            while (temp.Count > 0)
            {
                Recycle(temp.Dequeue());
            }
        }
        #endregion

        #region Querying 查询
        public static int CountPooled<T>(T prefab) where T : Component => CountPooled(prefab.gameObject);
        public static int CountPooled(GameObject prefab)
        {
            if (Instance.pooledObjects.TryGetValue(prefab, out Queue<GameObject> list))
                return list.Count;
            return 0;
        }
        public static int CountSpawned<T>(T prefab) where T : Component => CountSpawned(prefab.gameObject);
        public static int CountSpawned(GameObject prefab)
        {
            int count = 0;
            foreach (var instancePrefab in Instance.spawnedObjects.Values)
            {
                if (prefab == instancePrefab)
                {
                    ++count;
                }
            }
            return count;
        }

        public static int CountAllPooled()
        {
            int count = 0;
            foreach (var list in Instance.pooledObjects.Values)
                count += list.Count;
            return count;
        }

        public static List<GameObject> GetPooled(GameObject prefab, List<GameObject> list, bool appendList)
        {
            if (list == null)
                list = new List<GameObject>();
            if (!appendList)
                list.Clear();
            if (Instance.pooledObjects.TryGetValue(prefab, out Queue<GameObject> pooled))
                list.AddRange(pooled);
            return list;
        }
        public static List<T> GetPooled<T>(T prefab, List<T> list, bool appendList) where T : Component
        {
            if (list == null)
                list = new List<T>();
            if (!appendList)
                list.Clear();
            if (Instance.pooledObjects.TryGetValue(prefab.gameObject, out Queue<GameObject> pooled)) 
            {
                foreach (var item in pooled)
                {
                    list.Add(item.GetComponent<T>());
                }
            }
            return list;
        }

        public static List<GameObject> GetSpawned(GameObject prefab, List<GameObject> list, bool appendList)
        {
            if (list == null)
                list = new List<GameObject>();
            if (!appendList)
                list.Clear();
            foreach (var item in Instance.spawnedObjects)
                if (item.Value == prefab)
                    list.Add(item.Key);
            return list;
        }
        public static List<T> GetSpawned<T>(T prefab, List<T> list, bool appendList) where T : Component
        {
            if (list == null)
                list = new List<T>();
            if (!appendList)
                list.Clear();
            var prefabObj = prefab.gameObject;
            foreach (var item in Instance.spawnedObjects)
                if (item.Value == prefabObj)
                    list.Add(item.Key.GetComponent<T>());
            return list;
        }
        #endregion

        #region Destroy
        public static void DestroyPooled<T>(T prefab) where T : Component => DestroyPooled(prefab.gameObject);
        public static void DestroyAll<T>(T prefab) where T : Component => DestroyAll(prefab.gameObject);
        public static void DestroyPooled(GameObject prefab)
        {
            if (Instance.pooledObjects.TryGetValue(prefab, out Queue<GameObject> pooled))
            {
                while (pooled.Count>0)
                {
                    Destroy(pooled.Dequeue());
                }
            }
        }
        public static void DestroyAll(GameObject prefab)
        {
            RecycleAll(prefab);
            DestroyPooled(prefab);
        }
        #endregion
    }

    #region  Assistant Type
    /// <summary>
    /// 扩展方法所在
    /// </summary>
    public static class ObjectPoolExtensions
    {
        public static void CreatePool<T>(this T prefab) where T : Component => ObjectPool.CreatePool(prefab, 0);
        public static void CreatePool<T>(this T prefab, int initialPoolSize) where T : Component => ObjectPool.CreatePool(prefab, initialPoolSize);
        public static void CreatePool(this GameObject prefab) => ObjectPool.CreatePool(prefab, 0);
        public static void CreatePool(this GameObject prefab, int initialPoolSize) => ObjectPool.CreatePool(prefab, initialPoolSize);
        public static T Spawn<T>(this T prefab, Transform parent, Vector3 position, Quaternion rotation) where T : Component => ObjectPool.Spawn(prefab, parent, position, rotation);
        public static T Spawn<T>(this T prefab, Vector3 position, Quaternion rotation) where T : Component => ObjectPool.Spawn(prefab, null, position, rotation);
        public static T Spawn<T>(this T prefab, Transform parent, Vector3 position) where T : Component => ObjectPool.Spawn(prefab, parent, position, Quaternion.identity);
        public static T Spawn<T>(this T prefab, Vector3 position) where T : Component => ObjectPool.Spawn(prefab, null, position, Quaternion.identity);
        public static T Spawn<T>(this T prefab, Transform parent) where T : Component => ObjectPool.Spawn(prefab, parent, Vector3.zero, Quaternion.identity);
        public static T Spawn<T>(this T prefab) where T : Component => ObjectPool.Spawn(prefab, null, Vector3.zero, Quaternion.identity);
        public static GameObject Spawn(this GameObject prefab, Transform parent, Vector3 position, Quaternion rotation) => ObjectPool.Spawn(prefab, parent, position, rotation);
        public static GameObject Spawn(this GameObject prefab, Vector3 position, Quaternion rotation) => ObjectPool.Spawn(prefab, null, position, rotation);
        public static GameObject Spawn(this GameObject prefab, Transform parent, Vector3 position) => ObjectPool.Spawn(prefab, parent, position, Quaternion.identity);
        public static GameObject Spawn(this GameObject prefab, Vector3 position) => ObjectPool.Spawn(prefab, null, position, Quaternion.identity);
        public static GameObject Spawn(this GameObject prefab, Transform parent) => ObjectPool.Spawn(prefab, parent, Vector3.zero, Quaternion.identity);
        public static GameObject Spawn(this GameObject prefab) => ObjectPool.Spawn(prefab, null, Vector3.zero, Quaternion.identity);
        public static void Recycle<T>(this T obj) where T : Component => ObjectPool.Recycle(obj);
        public static void Recycle(this GameObject obj) => ObjectPool.Recycle(obj);
        public static void RecycleAll<T>(this T prefab) where T : Component => ObjectPool.RecycleAll(prefab);
        /// <summary>
        /// Recycle all objects instantiated from this prefab to the object pool.
        /// 将从此预制体实例化出的所有对象回收到对象池
        /// </summary>
        /// <param name="prefab">请指定预制体</param>
        public static void RecycleAll(this GameObject prefab) => ObjectPool.RecycleAll(prefab);
        public static int CountPooled<T>(this T prefab) where T : Component => ObjectPool.CountPooled(prefab);
        public static int CountPooled(this GameObject prefab) => ObjectPool.CountPooled(prefab);
        public static int CountSpawned<T>(this T prefab) where T : Component => ObjectPool.CountSpawned(prefab);
        public static int CountSpawned(this GameObject prefab) => ObjectPool.CountSpawned(prefab);
        public static List<GameObject> GetSpawned(this GameObject prefab, List<GameObject> list, bool appendList) => ObjectPool.GetSpawned(prefab, list, appendList);
        public static List<GameObject> GetSpawned(this GameObject prefab, List<GameObject> list) => ObjectPool.GetSpawned(prefab, list, false);
        public static List<GameObject> GetSpawned(this GameObject prefab) => ObjectPool.GetSpawned(prefab, null, false);
        public static List<T> GetSpawned<T>(this T prefab, List<T> list, bool appendList) where T : Component => ObjectPool.GetSpawned(prefab, list, appendList);
        public static List<T> GetSpawned<T>(this T prefab, List<T> list) where T : Component => ObjectPool.GetSpawned(prefab, list, false);
        public static List<T> GetSpawned<T>(this T prefab) where T : Component => ObjectPool.GetSpawned(prefab, null, false);
        public static List<GameObject> GetPooled(this GameObject prefab, List<GameObject> list, bool appendList) => ObjectPool.GetPooled(prefab, list, appendList);
        public static List<GameObject> GetPooled(this GameObject prefab, List<GameObject> list) => ObjectPool.GetPooled(prefab, list, false);
        public static List<GameObject> GetPooled(this GameObject prefab) => ObjectPool.GetPooled(prefab, null, false);
        public static List<T> GetPooled<T>(this T prefab, List<T> list, bool appendList) where T : Component => ObjectPool.GetPooled(prefab, list, appendList);
        public static List<T> GetPooled<T>(this T prefab, List<T> list) where T : Component => ObjectPool.GetPooled(prefab, list, false);
        public static List<T> GetPooled<T>(this T prefab) where T : Component => ObjectPool.GetPooled(prefab, null, false);

        /// <summary>
        ///  Destroy GameObject those has recycled into the pool
        ///  销毁回收到池中的游戏对象。
        /// </summary>
        /// <param name="prefab">请指定预制体</param>
        public static void DestroyPooled(this GameObject prefab) => ObjectPool.DestroyPooled(prefab);
        /// <summary>
        ///  Destroy GameObject those has recycled into the pool
        ///  销毁回收到池中的游戏对象。
        /// </summary>
        /// <typeparam name="T">池化的对象类型</typeparam>
        /// <param name="prefab">请指定预制体</param>
        public static void DestroyPooled<T>(this T prefab) where T : Component => ObjectPool.DestroyPooled(prefab.gameObject);
        public static void DestroyAll(this GameObject prefab) => ObjectPool.DestroyAll(prefab);
        public static void DestroyAll<T>(this T prefab) where T : Component => ObjectPool.DestroyAll(prefab.gameObject);
    }

    [System.Serializable]
    public class StartupPool
    {
        public int size;
        public GameObject prefab;
    }
    public enum StartupPoolMode
    {
        Awake,
        Start,
        Manually
    };


    #endregion
}
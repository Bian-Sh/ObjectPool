using UnityEngine;
using System.Collections.Generic;
namespace zFramework.Pool
{
    public sealed class ObjectPool : MonoBehaviour
    {
        #region Fileds
        public PoolInitMode poolInitMode;
        public StartupPool[] presetPools;
        static ObjectPool _instance;
        static Transform container;
        static Dictionary<GameObject, GameObject> spawnedObjects = new Dictionary<GameObject, GameObject>();
        static Dictionary<GameObject, Queue<GameObject>> pooledObjects = new Dictionary<GameObject, Queue<GameObject>>();
        #endregion

        #region Singleton and pre-instantiate
        public static ObjectPool Instance
        {
            get
            {
                _instance ??= FindObjectOfType<ObjectPool>();
                if (!_instance)
                {
                    new GameObject("[ObjectPool]", typeof(ObjectPool));
                }
                return _instance;
            }
        }
        void Awake()
        {
            if (_instance && _instance.gameObject != gameObject)
            {
                DestroyImmediate(gameObject);
            }
            else
            {
                _instance = this;
                var go = new GameObject("[PoolContainer]");
                container = go.transform;
                container.SetParent(transform);
                go.SetActive(false);
                go.hideFlags = HideFlags.HideInHierarchy;
                DontDestroyOnLoad(gameObject);
                if (poolInitMode == PoolInitMode.Awake)
                    CreateStartupPools();
            }
        }
        void Start()
        {
            if (poolInitMode == PoolInitMode.Start)
                CreateStartupPools();
        }

        public static void CreateStartupPools()
        {
            foreach (var pool in _instance.presetPools)
            {
                CreatePool(pool.prefab, pool.size);
            }
        }
        #endregion

        #region Pool Create
        public static void CreatePool<T>(T prefab, int initialPoolSize) where T : Component => CreatePool(prefab.gameObject, initialPoolSize);
        public static void CreatePool(GameObject prefab, int initialPoolSize)
        {
            if (prefab != null && !pooledObjects.ContainsKey(prefab))
            {
                var list = new Queue<GameObject>();
                pooledObjects.Add(prefab, list);

                if (initialPoolSize > 0)
                {
                    while (list.Count < initialPoolSize)
                    {
                        var obj = Instantiate(prefab, container, !(prefab.transform is RectTransform));
                        list.Enqueue(obj);
                    }
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
            if (pooledObjects.TryGetValue(prefab, out Queue<GameObject> list))  //1. check pool first
            {
                while (!obj && list.Count > 0) // in case of unexpected destroy 
                {
                    obj = list.Dequeue();
                }
            }
            if (!obj) obj = Instantiate(prefab); // 2. instantiate if not pooled
            obj.transform.SetParent(parent, !(obj.transform is RectTransform)); // 3. the second param should be false if the object is a ui component.
            obj.transform.localPosition = position;
            obj.transform.localRotation = rotation;
            if (pooledObjects.ContainsKey(prefab)) //4.only record spawned object if the prefab has been init before
            {
                spawnedObjects.Add(obj, prefab);
            }
            return obj;
        }
        #endregion

        #region Recyle
        public static void Recycle<T>(T instance) where T : Component => Recycle(instance.gameObject);
        public static void Recycle(GameObject instance)
        {
            if (pooledObjects.ContainsKey(instance))
            {
                Debug.LogError($"{nameof(ObjectPool)}: 你正在尝试回收预制体 {instance.name}，该行为不被支持！");
                return;
            }
            if (spawnedObjects.TryGetValue(instance, out GameObject prefab))
            {
                Recycle(instance, prefab);
            }
            else if (instance.transform.parent != container)
            {
                //约定位于 ObjectPool 子节点下表明已经回收
                // 否则不属于 ObjectPool 管理的对象，直接销毁
                Destroy(instance);
            }
        }
        static void Recycle(GameObject instance, GameObject prefab)
        {
            pooledObjects[prefab].Enqueue(instance);
            spawnedObjects.Remove(instance);
            instance.transform.SetParent(container, !(instance.transform is RectTransform));
            instance.SetActive(prefab.activeSelf);
        }

        public static void RecycleAll<T>(T prefab) where T : Component => RecycleAll(prefab.gameObject);
        public static void RecycleAll(GameObject prefab)
        {
            var temp = new Queue<GameObject>();
            foreach (var item in spawnedObjects)
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
            var temp = new Queue<GameObject>(spawnedObjects.Keys);
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
            if (pooledObjects.TryGetValue(prefab, out var list))
                return list.Count;
            return 0;
        }
        public static int CountSpawned<T>(T prefab) where T : Component => CountSpawned(prefab.gameObject);
        public static int CountSpawned(GameObject prefab)
        {
            int count = 0;
            foreach (var instancePrefab in spawnedObjects.Values)
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
            foreach (var list in pooledObjects.Values)
                count += list.Count;
            return count;
        }

        public static List<GameObject> GetPooled(GameObject prefab, List<GameObject> list, bool appendList)
        {
            list ??= new List<GameObject>();
            if (!appendList)
                list.Clear();
            if (pooledObjects.TryGetValue(prefab, out var pooled))
                list.AddRange(pooled);
            return list;
        }
        public static List<T> GetPooled<T>(T prefab, List<T> list, bool appendList) where T : Component
        {
            list ??= new List<T>();
            if (!appendList)
                list.Clear();
            if (pooledObjects.TryGetValue(prefab.gameObject, out var pooled))
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
            list ??= new List<GameObject>();
            if (!appendList)
                list.Clear();
            foreach (var item in spawnedObjects)
                if (item.Value == prefab)
                    list.Add(item.Key);
            return list;
        }
        public static List<T> GetSpawned<T>(T prefab, List<T> list, bool appendList) where T : Component
        {
            list ??= new List<T>();
            if (!appendList)
                list.Clear();
            foreach (var item in spawnedObjects)
                if (item.Value == prefab.gameObject)
                    list.Add(item.Key.GetComponent<T>());
            return list;
        }
        #endregion

        #region Destroy
        public static void DestroyPooled<T>(T prefab) where T : Component => DestroyPooled(prefab.gameObject);
        public static void DestroyAll<T>(T prefab) where T : Component => DestroyAll(prefab.gameObject);
        public static void DestroyPooled(GameObject prefab)
        {
            if (pooledObjects.TryGetValue(prefab, out Queue<GameObject> pooled))
            {
                while (pooled.Count > 0)
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
        public static void Recycle<T>(this T instance) where T : Component => ObjectPool.Recycle(instance);
        public static void Recycle(this GameObject instance) => ObjectPool.Recycle(instance);
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
    public enum PoolInitMode
    {
        Awake,
        Start,
        Manually
    };
    #endregion
}

using System.Collections.Generic;
using UnityEngine;

public class HexaSpawner : MonoBehaviour
{
    [SerializeField] private HexaItem hexaPrefab;
    private Queue<HexaItem> hexaPool = new Queue<HexaItem>();

    [SerializeField] private int poolSize = 20;

    private void Awake()
    {
        for (int i = 0; i < poolSize; i++)
        {
            var obj = Instantiate(hexaPrefab, transform);
            obj.transform.localPosition = Vector3.zero;
            obj.gameObject.SetActive(false);
            hexaPool.Enqueue(obj);
        }
    }

    public HexaItem GetFromPool()
    {
        if (hexaPool.Count == 0)
        {
            var newObj = Instantiate(hexaPrefab, transform);
            newObj.gameObject.SetActive(false);
            hexaPool.Enqueue(newObj);
        }

        var hexa = hexaPool.Dequeue();
        hexa.gameObject.SetActive(true);
        hexa.transform.localPosition = Vector3.zero;
        return hexa;
    }
    public void ReturnToPool(HexaItem hexa)
    {
        hexa.gameObject.SetActive(false);
        hexa.transform.SetParent(transform);
        hexaPool.Enqueue(hexa);
    }
    public void SpawnHexa(CellHexa cell, int number)
    {
        if (cell == null) return;

        Vector3 spawnPos = cell.transform.position + Vector3.up * 0.1f;
        var hexa = GetFromPool();

        hexa.transform.SetParent(cell.transform);
        hexa.transform.position = spawnPos;
        hexa.transform.rotation = cell.transform.rotation;
        hexa.Init(number);

        cell.SetItemHexa(hexa);
        hexa.SetCellHexa(cell);
    }
}

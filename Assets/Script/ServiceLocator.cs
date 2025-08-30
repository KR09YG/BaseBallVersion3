using System.Collections.Generic;
using UnityEngine;
using System;

public static class ServiceLocator
{
    static readonly Dictionary<Type, MonoBehaviour> services = new Dictionary<Type, MonoBehaviour>();

    public static void Register<T>(T service) where T : MonoBehaviour
    {
        var type = typeof(T);
        Debug.Log($"Registering service of type {type}");

        if (services.ContainsKey(type))
        {
            Debug.LogWarning($"Service of type {type} is already registered. Overwriting the existing service.");
            services[type] = service;
        }
        else
        {
            services.Add(type, service);
        }
    }

    public static T Get<T>() where T : MonoBehaviour
    {
        services.TryGetValue(typeof(T), out var service);
        return service as T;
    }
}

﻿using AzureNamingTool.Helpers;
using AzureNamingTool.Models;
using Microsoft.AspNetCore.Components;
using System;
using System.ComponentModel.Design;
using System.Linq;
using System.Security.AccessControl;
using System.Text;
using System.Text.Json;
using ResourceType = AzureNamingTool.Models.ResourceType;

namespace AzureNamingTool.Services
{
    public class ResourceTypeService
    {
        private static ServiceResponse serviceResponse = new();

        public static async Task<ServiceResponse> GetItems(bool admin = true)
        {
            try
            {
                // Get list of items
                var items = await ConfigurationHelper.GetList<ResourceType>();
                if (GeneralHelper.IsNotNull(items))
                {
                    if (!admin)
                    {
                        serviceResponse.ResponseObject = items.Where(x => x.Enabled == true).OrderBy(x => x.Resource).ToList();
                    }
                    else
                    {
                        serviceResponse.ResponseObject = items.OrderBy(x => x.Resource).ToList();
                    }
                    serviceResponse.Success = true;
                }
            }
            catch (Exception ex)
            {
                AdminLogService.PostItem(new AdminLogMessage() { Title = "ERROR", Message = ex.Message });
                serviceResponse.Success = false;
                serviceResponse.ResponseObject = ex;
            }
            return serviceResponse;
        }

        public static async Task<ServiceResponse> GetItem(int id)
        {
            try
            {
                // Get list of items
                var items = await ConfigurationHelper.GetList<ResourceType>();
                if (GeneralHelper.IsNotNull(items))
                {
                    var item = items.Find(x => x.Id == id);
                    if (GeneralHelper.IsNotNull(item))
                    {
                        serviceResponse.ResponseObject = item;
                        serviceResponse.Success = true;
                    }
                }
            }
            catch (Exception ex)
            {
                AdminLogService.PostItem(new AdminLogMessage() { Title = "ERROR", Message = ex.Message });
                serviceResponse.Success = false;
                serviceResponse.ResponseObject = ex;
            }
            return serviceResponse;
        }

        public static async Task<ServiceResponse> PostItem(ResourceType item)
        {
            try
            {
                // Make sure the new item short name only contains letters/numbers
                if (!ValidationHelper.CheckAlphanumeric(item.ShortName))
                {
                    serviceResponse.Success = false;
                    serviceResponse.ResponseObject = "Short name must be alphanumeric.";
                    return serviceResponse;
                }

                // Force lowercase on the shortname
                item.ShortName = item.ShortName.ToLower();

                // Get list of items
                var items = await ConfigurationHelper.GetList<ResourceType>();
                if (GeneralHelper.IsNotNull(items))
                {
                    // Set the new id
                    if (item.Id == 0)
                    {
                        item.Id = items.Count + 1;
                    }

                    // Determine new item id
                    if (items.Count > 0)
                    {
                        // Check if the item already exists
                        if (items.Exists(x => x.Id == item.Id))
                        {
                            // Remove the updated item from the list
                            var existingitem = items.Find(x => x.Id == item.Id);
                            if (GeneralHelper.IsNotNull(existingitem))
                            {
                                int index = items.IndexOf(existingitem);
                                items.RemoveAt(index);
                            }
                        }

                        // Put the item at the end
                        items.Add(item);
                    }
                    else
                    {
                        item.Id = 1;
                        items.Add(item);
                    }

                    // Write items to file
                    await ConfigurationHelper.WriteList<ResourceType>(items.OrderBy(x => x.Id).ToList());
                    serviceResponse.ResponseObject = "Item added!";
                    serviceResponse.Success = true;
                }
            }
            catch (Exception ex)
            {
                AdminLogService.PostItem(new AdminLogMessage() { Title = "ERROR", Message = ex.Message });
                serviceResponse.ResponseObject = ex;
                serviceResponse.Success = false;
            }
            return serviceResponse;
        }

        public static async Task<ServiceResponse> DeleteItem(int id)
        {
            try
            {
                // Get list of items
                var items = await ConfigurationHelper.GetList<ResourceType>();
                if (GeneralHelper.IsNotNull(items))
                {
                    // Get the specified item
                    var item = items.Find(x => x.Id == id);
                    if (GeneralHelper.IsNotNull(item))
                    {
                        // Remove the item from the collection
                        items.Remove(item);

                        // Write items to file
                        await ConfigurationHelper.WriteList<ResourceType>(items);
                        serviceResponse.Success = true;
                    }
                }
            }
            catch (Exception ex)
            {
                AdminLogService.PostItem(new AdminLogMessage() { Title = "ERROR", Message = ex.Message });
                serviceResponse.ResponseObject = ex;
                serviceResponse.Success = false;
            }
            return serviceResponse;
        }

        public static async Task<ServiceResponse> PostConfig(List<ResourceType> items)
        {
            try
            {
                // Get list of items
                var newitems = new List<ResourceType>();
                int i = 1;

                // Determine new item id
                foreach (ResourceType item in items)
                {
                    // Force lowercase on the shortname
                    item.ShortName = item.ShortName.ToLower();
                    item.Id = i;
                    newitems.Add(item);
                    i += 1;
                }

                // Write items to file
                await ConfigurationHelper.WriteList<ResourceType>(newitems);
                serviceResponse.Success = true;
            }
            catch (Exception ex)
            {
                AdminLogService.PostItem(new AdminLogMessage() { Title = "ERROR", Message = ex.Message });
                serviceResponse.Success = false;
                serviceResponse.ResponseObject = ex;
            }
            return serviceResponse;
        }

        public static List<string> GetTypeCategories(List<ResourceType> types)
        {
            List<string> categories = new();

            foreach (ResourceType type in types)
            {

                string category = type.Resource;
                if (!String.IsNullOrEmpty(category))
                {
                    if (category.Contains('/'))
                    {
                        category = category[..category.IndexOf("/")];
                    }

                    if (!categories.Contains(category))
                    {
                        categories.Add(category);
                    }
                }
            }

            return categories;
        }

        public static List<ResourceType> GetFilteredResourceTypes(List<ResourceType> types, string filter)
        {
            List<ResourceType> currenttypes = new();
            // Filter out resource types that should have name generation
            if (!String.IsNullOrEmpty(filter))
            {
                currenttypes = types.Where(x => x.Resource.ToLower().StartsWith(filter.ToLower() + "/") && x.Property.ToLower() != "display name" && !String.IsNullOrEmpty(x.ShortName)).ToList();
            }
            else
            {
                currenttypes = types;
            }
            return currenttypes;
        }

        public static async Task<ServiceResponse> RefreshResourceTypes(bool shortNameReset = false)
        {
            try
            {
                // Get the existing Resource Type items
                ServiceResponse serviceResponse;
                serviceResponse = await ResourceTypeService.GetItems();
                if (GeneralHelper.IsNotNull(serviceResponse.ResponseObject))
                {
                    List<ResourceType> types = (List<ResourceType>)serviceResponse.ResponseObject!;
                    if (GeneralHelper.IsNotNull(types))
                    {
                        string url = "https://raw.githubusercontent.com/mspnp/AzureNamingTool/main/repository/resourcetypes.json";
                        string refreshdata = await GeneralHelper.DownloadString(url);
                        if (!String.IsNullOrEmpty(refreshdata))
                        {
                            var newtypes = new List<ResourceType>();
                            var options = new JsonSerializerOptions
                            {
                                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                                PropertyNameCaseInsensitive = true
                            };

                            newtypes = JsonSerializer.Deserialize<List<ResourceType>>(refreshdata, options);
                            if (GeneralHelper.IsNotNull(newtypes))
                            {
                                // Loop through the new items
                                // Add any new resource type and update any existing types
                                foreach (ResourceType newtype in newtypes)
                                {
                                    // Check if the existing types contain the current type
                                    int i = types.FindIndex(x => x.Resource == newtype.Resource);
                                    if (i > -1)
                                    {
                                        // Update the Resource Type Information
                                        ResourceType oldtype = types[i];
                                        newtype.Exclude = oldtype.Exclude;
                                        newtype.Optional = oldtype.Optional;
                                        newtype.Enabled = oldtype.Enabled;
                                        if ((!shortNameReset) || (String.IsNullOrEmpty(oldtype.ShortName)))
                                        {
                                            newtype.ShortName = oldtype.ShortName;
                                        }
                                        // Remove the old type
                                        types.RemoveAt(i);
                                        // Add the new type
                                        types.Add(newtype);
                                    }
                                    else
                                    {
                                        // Add a new resource type
                                        types.Add(newtype);
                                    }
                                }

                                // Update the settings file
                                serviceResponse = await PostConfig(types);

                                // Update the repository file
                                await FileSystemHelper.WriteFile("resourcetypes.json", refreshdata, "repository/");

                                // Clear cached data
                                CacheHelper.InvalidateCacheObject("ResourceType");

                                // Update the current configuration file version data information
                                await ConfigurationHelper.UpdateConfigurationFileVersion("resourcetypes");
                            }
                        }
                        else
                        {
                            AdminLogService.PostItem(new AdminLogMessage() { Title = "ERROR", Message = "There was a problem refreshing the resource types configuration." });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                AdminLogService.PostItem(new AdminLogMessage() { Title = "ERROR", Message = ex.Message });
                serviceResponse.Success = false;
                serviceResponse.ResponseObject = ex;
            }
            return serviceResponse;
        }

        public static async Task<ServiceResponse> UpdateTypeComponents(string operation, int componentid)
        {
            try
            {
                serviceResponse = await ResourceComponentService.GetItem(componentid);
                if (GeneralHelper.IsNotNull(serviceResponse.ResponseObject))
                {
                    ResourceComponent resourceComponent = (ResourceComponent)serviceResponse.ResponseObject!;
                    if (GeneralHelper.IsNotNull(resourceComponent))
                    {
                        string component = GeneralHelper.NormalizeName(resourceComponent.Name, false);
                        serviceResponse = await ResourceTypeService.GetItems();
                        if (serviceResponse.Success)
                        {
                            if (GeneralHelper.IsNotNull(serviceResponse.ResponseObject))
                            {
                                List<ResourceType> resourceTypes = (List<ResourceType>)serviceResponse.ResponseObject!;
                                if (GeneralHelper.IsNotNull(resourceTypes))
                                {
                                    List<string> currentvalues = new();
                                    // Update all the resource type component settings
                                    foreach (ResourceType currenttype in resourceTypes)
                                    {
                                        switch (operation)
                                        {
                                            case "optional-add":
                                                currentvalues = new List<string>(currenttype.Optional.Split(','));
                                                if (!currentvalues.Contains(component))
                                                {
                                                    currentvalues.Add(component);
                                                    currenttype.Optional = String.Join(",", currentvalues.ToArray());
                                                    await ResourceTypeService.PostItem(currenttype);
                                                }
                                                break;
                                            case "optional-remove":
                                                currentvalues = new List<string>(currenttype.Optional.Split(','));
                                                if (currentvalues.Contains(component))
                                                {
                                                    currentvalues.Remove(component);
                                                    currenttype.Optional = String.Join(",", currentvalues.ToArray());
                                                    await ResourceTypeService.PostItem(currenttype);
                                                }
                                                break;
                                            case "exclude-add":
                                                currentvalues = new List<string>(currenttype.Exclude.Split(','));
                                                if (!currentvalues.Contains(component))
                                                {
                                                    currentvalues.Add(component);
                                                    currenttype.Exclude = String.Join(",", currentvalues.ToArray());
                                                    await ResourceTypeService.PostItem(currenttype);
                                                }
                                                break;
                                            case "exclude-remove":
                                                currentvalues = new List<string>(currenttype.Exclude.Split(','));
                                                if (currentvalues.Contains(component))
                                                {
                                                    currentvalues.Remove(component);
                                                    currenttype.Exclude = String.Join(",", currentvalues.ToArray());
                                                    await ResourceTypeService.PostItem(currenttype);
                                                }
                                                break;
                                        }
                                    }
                                    serviceResponse.Success = true;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                AdminLogService.PostItem(new AdminLogMessage() { Title = "ERROR", Message = ex.Message });
                serviceResponse.Success = false;
                serviceResponse.ResponseObject = ex;
            }
            return serviceResponse;
        }

        public static async Task<ServiceResponse> ValidateResourceTypeName(ValidateNameRequest validateNameRequest)
        {
            try
            {
                ServiceResponse serviceResponse = new();
                ResourceDelimiter? resourceDelimiter = new();
                // Get the current delimiter
                serviceResponse = await ResourceDelimiterService.GetCurrentItem();
                if (serviceResponse.Success)
                {
                    if (GeneralHelper.IsNotNull(serviceResponse.ResponseObject))
                    {
                        resourceDelimiter = serviceResponse.ResponseObject as ResourceDelimiter;
                    }
                }
                else
                {
                    serviceResponse.ResponseObject = "Delimiter value could not be set.";
                    serviceResponse.Success = false;
                    return serviceResponse;
                }

                // Get the specifed resource type
                serviceResponse = await ResourceTypeService.GetItems(true);
                if (serviceResponse.Success)
                {
                    if (GeneralHelper.IsNotNull(serviceResponse.ResponseObject))
                    {
                        // Get the resource types
                        List<ResourceType> resourceTypes = (List<ResourceType>)serviceResponse.ResponseObject!;
                        if (GeneralHelper.IsNotNull(resourceTypes))
                        {
                            // Get the specified resoure type
                            ResourceType resourceType = resourceTypes.FirstOrDefault(x => x.ShortName == validateNameRequest.ResourceType)!;
                            if (GeneralHelper.IsNotNull(resourceType))
                            {
                                // Create a validate name request
                                ValidateNameResponse validateNameResponse = ValidationHelper.ValidateGeneratedName(resourceType, validateNameRequest.Name!, resourceDelimiter!.Delimiter);
                                serviceResponse.ResponseObject = validateNameResponse;
                                serviceResponse.Success = true;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                AdminLogService.PostItem(new AdminLogMessage() { Title = "ERROR", Message = ex.Message });
                serviceResponse.Success = false;
                serviceResponse.ResponseObject = ex;
            }
            return serviceResponse;
        }
    }
}

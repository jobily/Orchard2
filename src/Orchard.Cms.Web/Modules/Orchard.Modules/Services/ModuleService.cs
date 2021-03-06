﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Localization;
using Orchard.DisplayManagement.Notify;
using Orchard.Environment.Extensions;
using Orchard.Environment.Extensions.Features;
using Orchard.Environment.Extensions.Models;
using Orchard.Environment.Shell.Descriptor;
using Orchard.Localization;
using Orchard.Modules.Models;

namespace Orchard.Modules.Services
{
    public class ModuleService : IModuleService
    {
        private readonly IFeatureManager _featureManager;
        private readonly IExtensionManager _extensionManager;
        private readonly IShellDescriptorManager _shellDescriptorManager;
        private readonly INotifier _notifier;

        public ModuleService(
                IFeatureManager featureManager,
                IExtensionManager extensionManager,
                IShellDescriptorManager shellDescriptorManager,
                IHtmlLocalizer<AdminMenu> htmlLocalizer,
                INotifier notifier)
        {
            _notifier = notifier;
            _featureManager = featureManager;
            _extensionManager = extensionManager;
            _shellDescriptorManager = shellDescriptorManager;

            //if (_featureManager.FeatureDependencyNotification == null) {
            //    _featureManager.FeatureDependencyNotification = GenerateWarning;
            //}

            T = htmlLocalizer;
        }

        public IHtmlLocalizer T { get; set; }

        /// <summary>
        /// Retrieves an enumeration of the available features together with its state (enabled / disabled).
        /// </summary>
        /// <returns>An enumeration of the available features together with its state (enabled / disabled).</returns>
        public async Task<IEnumerable<ModuleFeature>> GetAvailableFeatures()
        {
            var currentShellDescriptor = await _shellDescriptorManager.GetShellDescriptorAsync();
            var enabledFeatures = currentShellDescriptor.Features;
            return _extensionManager.AvailableExtensions()
                .SelectMany(m => _extensionManager.LoadFeatures(m.Features))
                .Select(f => AssembleModuleFromDescriptor(f, enabledFeatures
                    .FirstOrDefault(sf => string.Equals(sf.Name, f.Descriptor.Id, StringComparison.OrdinalIgnoreCase)) != null));
        }

        /// <summary>
        /// Enables a list of features.
        /// </summary>
        /// <param name="featureIds">The IDs for the features to be enabled.</param>
        public void EnableFeatures(IEnumerable<string> featureIds)
        {
            EnableFeatures(featureIds, false);
        }

        /// <summary>
        /// Enables a list of features.
        /// </summary>
        /// <param name="featureIds">The IDs for the features to be enabled.</param>
        /// <param name="force">Boolean parameter indicating if the feature should enable it's dependencies if required or fail otherwise.</param>
        public async void EnableFeatures(IEnumerable<string> featureIds, bool force)
        {
            var features = await _featureManager.EnableFeaturesAsync(featureIds, force);
            foreach (string featureId in features)
            {
                var availableFeatures = await _featureManager.GetAvailableFeaturesAsync();
                var featureName = availableFeatures.First(f => f.Id.Equals(featureId, StringComparison.OrdinalIgnoreCase)).Name;
                _notifier.Success(T["{0} was enabled", featureName]);
            }
        }

        /// <summary>
        /// Disables a list of features.
        /// </summary>
        /// <param name="featureIds">The IDs for the features to be disabled.</param>
        public void DisableFeatures(IEnumerable<string> featureIds)
        {
            DisableFeatures(featureIds, false);
        }

        /// <summary>
        /// Disables a list of features.
        /// </summary>
        /// <param name="featureIds">The IDs for the features to be disabled.</param>
        /// <param name="force">Boolean parameter indicating if the feature should disable the features which depend on it if required or fail otherwise.</param>
        public async void DisableFeatures(IEnumerable<string> featureIds, bool force)
        {
            var features = await _featureManager.DisableFeaturesAsync(featureIds, force);
            foreach (string featureId in features)
            {
                var availableFeatures = await _featureManager.GetAvailableFeaturesAsync();
                var featureName = availableFeatures.Single(f => f.Id.Equals(featureId, StringComparison.OrdinalIgnoreCase)).Name;
                _notifier.Success(T["{0} was disabled", featureName]);
            }
        }

        ///// <summary>
        ///// Determines if a module was recently installed by using the project's last written time.
        ///// </summary>
        ///// <param name="extensionDescriptor">The extension descriptor.</param>
        //public bool IsRecentlyInstalled(ExtensionDescriptor extensionDescriptor) {
        //    DateTime lastWrittenUtc = _cacheManager.Get(extensionDescriptor, descriptor => {
        //        string projectFile = GetManifestPath(extensionDescriptor);
        //        if (!string.IsNullOrEmpty(projectFile)) {
        //            // If project file was modified less than 24 hours ago, the module was recently deployed
        //            return _virtualPathProvider.GetFileLastWriteTimeUtc(projectFile);
        //        }

        //        return DateTime.UtcNow;
        //    });

        //    return DateTime.UtcNow.Subtract(lastWrittenUtc) < new TimeSpan(1, 0, 0, 0);
        //}

        public IEnumerable<FeatureDescriptor> GetDependentFeatures(string featureId)
        {
            return Task.Run(() => GetDependentFeaturesAsync(featureId)).Result;
        }

        public async Task<IEnumerable<FeatureDescriptor>> GetDependentFeaturesAsync(string featureId)
        {
            var dependants = await _featureManager.GetDependentFeaturesAsync(featureId);
            var availableFeatures = await _featureManager.GetAvailableFeaturesAsync();
            var availableFeaturesLookup = availableFeatures.ToLookup(f => f.Id, StringComparer.OrdinalIgnoreCase);

            return dependants
                .SelectMany(id => availableFeaturesLookup[id])
                .ToList();
        }

        ///// <summary>
        ///// Retrieves the full path of the manifest file for a module's extension descriptor.
        ///// </summary>
        ///// <param name="extensionDescriptor">The module's extension descriptor.</param>
        ///// <returns>The full path to the module's manifest file.</returns>
        //private string GetManifestPath(ExtensionDescriptor extensionDescriptor) {
        //    string projectPath = _virtualPathProvider.Combine(extensionDescriptor.Location, extensionDescriptor.Id, "module.txt");

        //    if (!_virtualPathProvider.FileExists(projectPath)) {
        //        return null;
        //    }

        //    return projectPath;
        //}

        private static ModuleFeature AssembleModuleFromDescriptor(Feature feature, bool isEnabled)
        {
            return new ModuleFeature
            {
                Descriptor = feature.Descriptor,
                IsEnabled = isEnabled
            };
        }

        //private void GenerateWarning(string messageFormat, string featureName, IEnumerable<string> featuresInQuestion) {
        //    if (featuresInQuestion.Count() < 1)
        //        return;

        //    Services.Notifier.Warning(T(
        //        messageFormat,
        //        featureName,
        //        featuresInQuestion.Count() > 1
        //            ? string.Join("",
        //                          featuresInQuestion.Select(
        //                              (fn, i) =>
        //                              T(i == featuresInQuestion.Count() - 1
        //                                    ? "{0}"
        //                                    : (i == featuresInQuestion.Count() - 2
        //                                           ? "{0} and "
        //                                           : "{0}, "), fn).ToString()).ToArray())
        //            : featuresInQuestion.First()));
        //}
    }
}
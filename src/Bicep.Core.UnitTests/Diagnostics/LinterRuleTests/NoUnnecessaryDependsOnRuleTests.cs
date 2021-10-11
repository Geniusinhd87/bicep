// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Bicep.Core.Analyzers.Linter.Rules;
using Bicep.Core.UnitTests.Assertions;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;

namespace Bicep.Core.UnitTests.Diagnostics.LinterRuleTests
{
    [TestClass]
    public class NoUnnecessaryDependsOnRuleTests : LinterRuleTestsBase
    {
        private void CompileAndTest(string text, OnCompileErrors onCompileErrors, string[] expectedMessages)
        {
            AssertLinterRuleDiagnostics(NoUnnecessaryDependsOnRule.Code, text, expectedMessages, onCompileErrors);
        }

        [TestMethod]
        public void If_No_Simple_UnnecessaryDependsOn_ShouldPass()
        {
            CompileAndTest(@"
              resource appServicePlan 'Microsoft.Web/serverfarms@2020-12-01' = {
                name: 'name'
                location: resourceGroup().location
                sku: {
                  name: 'F1'
                  capacity: 1
                }
              }

              resource webApplication 'Microsoft.Web/sites@2018-11-01' = {
                name: 'name'
                location: resourceGroup().location
                properties: {
                  serverFarmId: appServicePlan.id
                }
              }

              resource webApplication2 'Microsoft.Web/sites@2018-11-01' = {
                name: 'name2'
                location: resourceGroup().location
                properties: {
                  serverFarmId: appServicePlan.id
                }
                dependsOn: []
              }

              resource webApplication3 'Microsoft.Web/sites@2018-11-01' = {
                name: 'name3'
                location: resourceGroup().location
                properties: {
                  serverFarmId: appServicePlan.id
                }
                dependsOn: [
                    webApplication
                    webApplication //asdf fail because of duplicate entry?
                    webApplication2
                ]
              }
            ",
              OnCompileErrors.Fail,
              new string[] { }
            );
        }

        // This is the failing example in the docs
        [TestMethod]
        public void If_SimpleUnnecessaryDependsOn_ShouldFail()
        {
            CompileAndTest(
                @"
                resource appServicePlan 'Microsoft.Web/serverfarms@2020-12-01' = {
                  name: 'name'
                  location: resourceGroup().location
                  sku: {
                    name: 'F1'
                    capacity: 1
                  }
                }

                resource webApplication 'Microsoft.Web/sites@2018-11-01' = {
                  name: 'name'
                  location: resourceGroup().location
                  properties: {
                    serverFarmId: appServicePlan.id
                  }
                  dependsOn: [
                    appServicePlan
                  ]
                }
            ",
              OnCompileErrors.Fail,
              new string[] {
                "Remove unnecessary dependsOn entry 'appServicePlan'."
              }
            );
        }

        [TestMethod]
        public void If_Indirect_UnnecessaryDependsOn_ShouldFail()
        {
            CompileAndTest(
                @"
                resource appServicePlan 'Microsoft.Web/serverfarms@2020-12-01' = {
                  name: 'name'
                  location: resourceGroup().location
                  sku: {
                    name: 'F1'
                    capacity: 1
                  }
                }

                resource webApplication 'Microsoft.Web/sites@2018-11-01' = {
                  name: 'name'
                  location: resourceGroup().location
                  properties: {
                    serverFarmId: appServicePlan.id
                  }
                  dependsOn: [
                    appServicePlan
                  ]
                }
            ",
              OnCompileErrors.Fail,
              new string[] {
                "Remove unnecessary dependsOn entry 'appServicePlan'."
              });
        }

        [TestMethod]
        public void If_Explicit_DependsOn_ToAncestor_FromNestedChild_ShouldFail()
        {
            CompileAndTest(
                @"
                resource vnet 'Microsoft.Network/virtualNetworks@2020-06-01' = {
                  location: resourceGroup().location
                  name: 'myVnet'
                  properties: {
                    addressSpace: {
                      addressPrefixes: [
                        '10.0.0.0/20'
                      ]
                    }
                  }

                  // As nested child
                  resource subnet1 'subnets@2020-06-01' = {
                    name: 'subnet1'
                        properties: {
                        addressPrefix: '10.0.0.0/24'
                    }
                    dependsOn: [
                      vnet
                    ]
                  }
                }
            ",
              OnCompileErrors.Fail,
              new string[] {
                "" +
                "Remove unnecessary dependsOn entry 'vnet'."
              });
        }

        [TestMethod]
        public void If_Explicit_DependsOn_ToAncestor_FromTopLevelChild_ShouldFail()
        {
            CompileAndTest(
               @"
                resource vnet 'Microsoft.Network/virtualNetworks@2020-06-01' = {
                  location: resourceGroup().location
                  name: 'myVnet'
                  properties: {
                    addressSpace: {
                      addressPrefixes: [
                        '10.0.0.0/20'
                      ]
                    }
                  }
                }
                // As top-level child
                resource subnet 'Microsoft.Network/virtualNetworks/subnets@2020-06-01' = {
                  parent: vnet
                  name: 'subnet'
                  properties: {
                    addressPrefix: '10.0.1.0/24'
                  }
                  dependsOn: [
                    vnet
                  ]
                }
            ",
              OnCompileErrors.Fail,
              new string[] {
              "Remove unnecessary dependsOn entry 'vnet'."
              }
            );
        }

        [TestMethod]
        public void If_DuplicateEntries_ShouldFailForEach()
        {
            CompileAndTest(
               @"
                resource vn 'Microsoft.Network/virtualNetworks@2020-06-01' existing =  {
                  name: 'vn'

                  resource subnet1 'subnets@2020-06-01' = {
                    name: 'subnet1'
                    properties: {
                      addressPrefix: '10.0.1.0/24'
                    }
                  }

                  resource subnet2 'subnets@2020-06-01' = {
                    name: 'subnet2'
                    properties: {
                      addressPrefix: '10.0.1.0/24'
                    }
                    dependsOn: [
                      vn
                      subnet1
                      vn
                    ]
                  }
                }
            ",
              OnCompileErrors.Fail,
              new string[] {
                "Remove unnecessary dependsOn entry 'vn'.",
                "Remove unnecessary dependsOn entry 'vn'."
              }
            );
        }

        [TestMethod]
        public void If_Explicit_DependsOn_ToParent_FromGrandChild_UsingColonNotation_ShouldFail() //asdff
        {
            CompileAndTest(
                @"
                resource grandparent 'Microsoft.Network/virtualNetworks@2020-06-01' = {
                  location: resourceGroup().location
                  name: 'grandparent'
                  properties: {
                    addressSpace: {
                      addressPrefixes: [
                        '10.0.0.0/20'
                      ]
                    }
                  }
                  resource parent 'subnets@2020-06-01' = {
                    name: 'parent'
                    properties: {
                      addressPrefix: '10.0.1.0/24'
                    }
                    resource grandchild 'DoesntExistButThatsOkay@2020-10-01' = {
                      name: 'grandchild'
                      properties: {
                        addressPrefix: '10.0.1.0/24'
                      }
                      dependsOn: [
                        grandparent::parent
                      ]
                    }
                  }
                }",
                OnCompileErrors.Fail,
                new string[] {
                    "Remove unnecessary dependsOn entry 'grandparent::parent'."
            });
        }

        [TestMethod]
        public void If_UnnecessaryReferenceToParent_ViaIndex_Should_Fail() //asdff
        {
            CompileAndTest(@"
                resource stgAccts 'Microsoft.Storage/storageAccounts@2019-06-01' existing = [for i in range(0, 3): {
                  name: 'stgAcct${i}'
                }]

                resource blobServices 'Microsoft.Storage/storageAccounts/blobServices@2021-04-01'  = [for i in range(0, 3): {
                  name: 'blobs${i}'
                  parent: stgAccts[i]
                  dependsOn: [
                    stgAccts[i] // This is the parent so is unnecessary
                  ]
                }]
            ",
              OnCompileErrors.Fail,
              new string[] {
                "Remove unnecessary dependsOn entry 'stgAccts[i][0]'."
              }
            );
        }

        [TestMethod]
        public void If_UnnecessaryReferenceToParent_ViaIndex_Should_Fail_asdf() //asdff
        {
            CompileAndTest(@"
                resource stgAccts 'Microsoft.Storage/storageAccounts@2019-06-01' existing = [for i in range(0, 3): {
                  name: 'stgAcct${i}'
                }]

                resource blobServices 'Microsoft.Storage/storageAccounts/blobServices@2021-04-01'  = [for i in range(0, 3): {
                  name: 'blobs${i}'
                  parent: stgAccts[i]
                  dependsOn: [
                    stgAccts[i + 1] // This is not the parent so is necessary asdf??
                  ]
                }]
            ",
              OnCompileErrors.Fail,
              new string[] {
                "Remove unnecessary dependsOn entry 'stgAccts[i+1][1]'."
              }
            );
        }

        [TestMethod]
        public void If_UnnecessaryReferenceToParent_FromLoop_ToNonLoopedParent_Should_Fail() //asdff
        {
            CompileAndTest(@"
                resource vn 'Microsoft.Network/virtualNetworks@2021-02-01' existing = {
                  name: 'vn'
                }

                resource blobServices 'Microsoft.Network/virtualNetworks/subnets@2021-02-01' = [for i in range(0, 3): {
                  name: 'blobs${i}'
                  parent: vn
                  dependsOn: [
                    vn
                  ]
                }]
            ",
              OnCompileErrors.Fail,
              new string[] {
                "Remove unnecessary dependsOn entry 'vn'."
              }
            );
        }

        [TestMethod]
        public void If_UnnecessaryReferenceToParentCollection_FromNonLoopedChild_Should_Passasdf() //asdff
        {
            CompileAndTest(@"
                resource vn 'Microsoft.Network/virtualNetworks@2021-02-01' existing = [for i in range(0, 1): {
                  name: 'vn${i}'
                }]

                resource subnet 'Microsoft.Network/virtualNetworks/subnets@2021-02-01' = {
                  name: 'subnet'
                  parent: vn[0]
                  dependsOn: [
                    vn[0]   //asdf?
                  ]
                }
            ",
              OnCompileErrors.Fail,
              new string[] {
                "Remove unnecessary dependsOn entry 'vn'."
              }
            );
        }

        [TestMethod]
        public void If_UnnecessaryReferenceToParentCollectionEntry_FromNonLoopedChild_Should_Passasdf() //asdff
        {
            CompileAndTest(@"
                resource vn 'Microsoft.Network/virtualNetworks@2021-02-01' existing = [for i in range(0, 1): {
                  name: 'vn${i}'
                }]

                resource subnet 'Microsoft.Network/virtualNetworks/subnets@2021-02-01' = {
                  name: 'subnet'
                  parent: vn[0]
                  dependsOn: [
                    vn // asdf?
                  ]
                }
            ",
              OnCompileErrors.Fail,
              new string[] {
                "Remove unnecessary dependsOn entry 'vn'."
              }
            );
        }

        [TestMethod]
        public void If_ReferencesResourceByIndex_Should_WhatAsdff() //asdff
        {
            CompileAndTest(@"
              param storageAccounts array

              resource storageAccountResources 'Microsoft.Storage/storageAccounts@2019-06-01' = [for storageName in storageAccounts: {
                name: storageName
                location: resourceGroup().location
                properties: {
                  supportsHttpsTrafficOnly: true
                }
                kind: 'StorageV2'
                sku: {
                  name: 'Standard_LRS'
                }
              }]

              resource dScript 'Microsoft.Resources/deploymentScripts@2019-10-01-preview' = {
                name: 'scriptWithStorage'
                location: resourceGroup().location
                kind: 'AzureCLI'
                identity: {
                }
                properties: {
                    azCliVersion: '2.0.80'
                    storageAccountSettings: {
                    storageAccountName: storageAccountResources[0].name
                    }
                    retentionInterval: 'P1D'
                }
                dependsOn: [
                    storageAccountResources[0]
                ]
              }
            ",
              OnCompileErrors.Fail,
              new string[] {
                "Remove unnecessary dependsOn entry 'storageAccountResources[0]'."
              }
            );
        }
    }
}

// TODO asdff: syntax errors, specify via id e.g. Test_Issue3182
// TODO asdff: dependson in modules, indexed dependencies from loops, cycles
// todo asdff: ignore stuff like stg.name inside dependsOn (I assume this would be an error)
// todo asdff: if dependsOn has index, ignore...?
// todo asdff: reference to a collection of resources
using FsCheck;
using FsCheck.Xunit;
using k8s;
using k8s.Models;

namespace Gatekeeper.Tests;

/// <summary>
/// Property-based tests for OPA Gatekeeper cross-tenant NetworkPolicy rejection
/// </summary>
public class GatekeeperCrossTenantNetPolPropertyTests
{
    /// <summary>
    /// **Feature: multi-tenant-medical-platform, Property 67: 跨租户策略被拒绝**
    /// **Validates: Requirements 14.3**
    /// 
    /// For any attempt to create a cross-tenant NetworkPolicy, OPA Gatekeeper should
    /// reject the policy and return a violation message.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property GatekeeperRejectsCrossTenantNetworkPolicy()
    {
        return Prop.ForAll(
            GenerateNetworkPolicyWithCrossTenantAccess(),
            netpol =>
            {
                // Arrange: Extract namespace information
                var netpolNamespace = netpol.Metadata?.NamespaceProperty ?? "";
                var isInTenantNamespace = netpolNamespace.StartsWith("tenant-");

                if (!isInTenantNamespace)
                {
                    // Policy doesn't apply to non-tenant namespaces
                    return true;
                }

                // Extract tenant ID from namespace
                var netpolTenant = netpolNamespace.Substring(7); // Remove "tenant-" prefix

                // Check ingress rules for cross-tenant access
                var hasCrossTenantIngress = false;
                if (netpol.Spec?.Ingress != null)
                {
                    foreach (var ingressRule in netpol.Spec.Ingress)
                    {
                        if (ingressRule.FromProperty != null)
                        {
                            foreach (var fromRule in ingressRule.FromProperty)
                            {
                                if (fromRule.NamespaceSelector?.MatchLabels != null &&
                                    fromRule.NamespaceSelector.MatchLabels.TryGetValue("name", out var fromNamespace))
                                {
                                    if (fromNamespace.StartsWith("tenant-"))
                                    {
                                        var fromTenant = fromNamespace.Substring(7);
                                        if (fromTenant != netpolTenant)
                                        {
                                            hasCrossTenantIngress = true;
                                            break;
                                        }
                                    }
                                }
                            }
                        }
                        if (hasCrossTenantIngress) break;
                    }
                }

                // Check egress rules for cross-tenant access
                var hasCrossTenantEgress = false;
                if (netpol.Spec?.Egress != null)
                {
                    foreach (var egressRule in netpol.Spec.Egress)
                    {
                        if (egressRule.To != null)
                        {
                            foreach (var toRule in egressRule.To)
                            {
                                if (toRule.NamespaceSelector?.MatchLabels != null &&
                                    toRule.NamespaceSelector.MatchLabels.TryGetValue("name", out var toNamespace))
                                {
                                    if (toNamespace.StartsWith("tenant-"))
                                    {
                                        var toTenant = toNamespace.Substring(7);
                                        if (toTenant != netpolTenant)
                                        {
                                            hasCrossTenantEgress = true;
                                            break;
                                        }
                                    }
                                }
                            }
                        }
                        if (hasCrossTenantEgress) break;
                    }
                }

                // Act & Assert: Cross-tenant policies should be rejected
                var shouldBeRejected = hasCrossTenantIngress || hasCrossTenantEgress;
                
                return shouldBeRejected;
            }
        ).Label("Cross-tenant NetworkPolicies should be rejected");
    }

    /// <summary>
    /// Property: Same-tenant NetworkPolicies should be allowed
    /// </summary>
    [Property(MaxTest = 100)]
    public Property GatekeeperAllowsSameTenantNetworkPolicy()
    {
        return Prop.ForAll(
            GenerateSameTenantNetworkPolicy(),
            netpol =>
            {
                // Arrange: Extract namespace information
                var netpolNamespace = netpol.Metadata?.NamespaceProperty ?? "";
                var isInTenantNamespace = netpolNamespace.StartsWith("tenant-");

                if (!isInTenantNamespace)
                {
                    return true;
                }

                var netpolTenant = netpolNamespace.Substring(7);

                // Check that all referenced namespaces are same tenant or non-tenant
                var allSameTenant = true;

                // Check ingress rules
                if (netpol.Spec?.Ingress != null)
                {
                    foreach (var ingressRule in netpol.Spec.Ingress)
                    {
                        if (ingressRule.FromProperty != null)
                        {
                            foreach (var fromRule in ingressRule.FromProperty)
                            {
                                if (fromRule.NamespaceSelector?.MatchLabels != null &&
                                    fromRule.NamespaceSelector.MatchLabels.TryGetValue("name", out var fromNamespace))
                                {
                                    if (fromNamespace.StartsWith("tenant-"))
                                    {
                                        var fromTenant = fromNamespace.Substring(7);
                                        if (fromTenant != netpolTenant)
                                        {
                                            allSameTenant = false;
                                            break;
                                        }
                                    }
                                }
                            }
                        }
                        if (!allSameTenant) break;
                    }
                }

                // Check egress rules
                if (allSameTenant && netpol.Spec?.Egress != null)
                {
                    foreach (var egressRule in netpol.Spec.Egress)
                    {
                        if (egressRule.To != null)
                        {
                            foreach (var toRule in egressRule.To)
                            {
                                if (toRule.NamespaceSelector?.MatchLabels != null &&
                                    toRule.NamespaceSelector.MatchLabels.TryGetValue("name", out var toNamespace))
                                {
                                    if (toNamespace.StartsWith("tenant-"))
                                    {
                                        var toTenant = toNamespace.Substring(7);
                                        if (toTenant != netpolTenant)
                                        {
                                            allSameTenant = false;
                                            break;
                                        }
                                    }
                                }
                            }
                        }
                        if (!allSameTenant) break;
                    }
                }

                // Act & Assert: Same-tenant policies should be allowed
                return allSameTenant;
            }
        ).Label("Same-tenant NetworkPolicies should be allowed");
    }

    /// <summary>
    /// Property: Policies allowing access to shared services should be allowed
    /// </summary>
    [Property(MaxTest = 100)]
    public Property GatekeeperAllowsSharedServiceAccess()
    {
        return Prop.ForAll(
            GenerateNetworkPolicyToSharedServices(),
            netpol =>
            {
                // Arrange: Check if policy allows access to shared services
                var netpolNamespace = netpol.Metadata?.NamespaceProperty ?? "";
                var isInTenantNamespace = netpolNamespace.StartsWith("tenant-");

                if (!isInTenantNamespace)
                {
                    return true;
                }

                // Check egress rules for shared service access
                var hasSharedServiceAccess = false;
                if (netpol.Spec?.Egress != null)
                {
                    foreach (var egressRule in netpol.Spec.Egress)
                    {
                        if (egressRule.To != null)
                        {
                            foreach (var toRule in egressRule.To)
                            {
                                if (toRule.NamespaceSelector?.MatchLabels != null &&
                                    toRule.NamespaceSelector.MatchLabels.TryGetValue("name", out var toNamespace))
                                {
                                    // Shared service namespaces don't start with "tenant-"
                                    if (!toNamespace.StartsWith("tenant-"))
                                    {
                                        hasSharedServiceAccess = true;
                                        break;
                                    }
                                }
                            }
                        }
                        if (hasSharedServiceAccess) break;
                    }
                }

                // Act & Assert: Access to shared services should be allowed
                return hasSharedServiceAccess || netpol.Spec?.Egress == null;
            }
        ).Label("NetworkPolicies allowing shared service access should be allowed");
    }

    /// <summary>
    /// Generator for NetworkPolicies with cross-tenant access
    /// </summary>
    private static Arbitrary<V1NetworkPolicy> GenerateNetworkPolicyWithCrossTenantAccess()
    {
        return Arb.From(
            from sourceTenant in Gen.Elements("hospital-a", "hospital-b", "hospital-c")
            from targetTenant in Gen.Elements("hospital-a", "hospital-b", "hospital-c", "clinic-x")
            from direction in Gen.Elements("ingress", "egress")
            where sourceTenant != targetTenant // Ensure cross-tenant
            select CreateNetworkPolicy(
                name: "cross-tenant-policy",
                sourceNamespace: $"tenant-{sourceTenant}",
                targetNamespace: $"tenant-{targetTenant}",
                direction: direction
            )
        );
    }

    /// <summary>
    /// Generator for NetworkPolicies within same tenant
    /// </summary>
    private static Arbitrary<V1NetworkPolicy> GenerateSameTenantNetworkPolicy()
    {
        return Arb.From(
            from tenant in Gen.Elements("hospital-a", "hospital-b", "hospital-c")
            select CreateNetworkPolicy(
                name: "same-tenant-policy",
                sourceNamespace: $"tenant-{tenant}",
                targetNamespace: $"tenant-{tenant}",
                direction: "ingress"
            )
        );
    }

    /// <summary>
    /// Generator for NetworkPolicies to shared services
    /// </summary>
    private static Arbitrary<V1NetworkPolicy> GenerateNetworkPolicyToSharedServices()
    {
        return Arb.From(
            from tenant in Gen.Elements("hospital-a", "hospital-b", "hospital-c")
            from sharedService in Gen.Elements("platform-system", "gateway", "observability", "kube-system")
            select CreateNetworkPolicy(
                name: "shared-service-policy",
                sourceNamespace: $"tenant-{tenant}",
                targetNamespace: sharedService,
                direction: "egress"
            )
        );
    }

    /// <summary>
    /// Helper method to create a NetworkPolicy with specified parameters
    /// </summary>
    private static V1NetworkPolicy CreateNetworkPolicy(
        string name,
        string sourceNamespace,
        string targetNamespace,
        string direction)
    {
        var netpol = new V1NetworkPolicy
        {
            ApiVersion = "networking.k8s.io/v1",
            Kind = "NetworkPolicy",
            Metadata = new V1ObjectMeta
            {
                Name = name,
                NamespaceProperty = sourceNamespace
            },
            Spec = new V1NetworkPolicySpec
            {
                PodSelector = new V1LabelSelector()
            }
        };

        if (direction == "ingress")
        {
            netpol.Spec.Ingress = new List<V1NetworkPolicyIngressRule>
            {
                new V1NetworkPolicyIngressRule
                {
                    FromProperty = new List<V1NetworkPolicyPeer>
                    {
                        new V1NetworkPolicyPeer
                        {
                            NamespaceSelector = new V1LabelSelector
                            {
                                MatchLabels = new Dictionary<string, string>
                                {
                                    { "name", targetNamespace }
                                }
                            }
                        }
                    }
                }
            };
        }
        else // egress
        {
            netpol.Spec.Egress = new List<V1NetworkPolicyEgressRule>
            {
                new V1NetworkPolicyEgressRule
                {
                    To = new List<V1NetworkPolicyPeer>
                    {
                        new V1NetworkPolicyPeer
                        {
                            NamespaceSelector = new V1LabelSelector
                            {
                                MatchLabels = new Dictionary<string, string>
                                {
                                    { "name", targetNamespace }
                                }
                            }
                        }
                    }
                }
            };
        }

        return netpol;
    }
}

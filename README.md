# GitOps Deployment Files

> Kubernetes components deployed via Flux for `AKS Secure Baseline`

- ocw-baseline - baseline components for all clusters
- deploy/clusterName - cluster specific deployment files
- cluster-manifests - `deprecated`

## Flux Setup

```yaml

args:
- --git-url=https://github.com/retaildevcrews/gitops.git
- --git-branch=gitops                             ### todo - potentially change this
- --git-path=ocw-baseline,deploy/yourClusterName  ### todo - change this
- --git-readonly
- --sync-state=secret
- --listen-metrics=:3031
- --git-timeout=30s
- --registry-disable-scanning=true


```

### Cluster Specific Files

- cluster specific `traefik` config
  - /deploy/yourClusterName/ingress/traefik-config.yaml
- app specific `traefik` config
  - /deploy/yourClusterName/ngsa/ngsa-ingress.yaml

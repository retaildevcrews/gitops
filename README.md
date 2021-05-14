# GitOps Deployment Files

## Do not merge this branch

> Kubernetes components deployed via Flux for `AKS Secure Baseline`

This is an incomplete sample of deploying `Azure Secure Baseline` with `AutoGitOps`

- ocw-baseline - baseline components for all clusters
- deploy/bartr1 - cluster specific deployment files

## Flux Setup

```yaml

args:
- --git-url=https://github.com/retaildevcrews/gitops.git
- --git-branch=bartr                      ### todo - change this
- --git-path=ocw-baseline,deploy/bartr    ### todo - change this
- --git-readonly
- --sync-state=secret
- --listen-metrics=:3031
- --git-timeout=30s
- --registry-disable-scanning=true


```

### Cluster Specific Files

> These are the files generated during the ASB deployment lab

- cluster specific `traefik` config
  - /deploy/bartr1/ingress/traefik-config.yaml
- app specific `traefik` config
  - /deploy/bartr1/ngsa/ngsa-ingress.yaml

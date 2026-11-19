# Deploying to AWS (Free Tier)

Target: **one EC2 `t3.micro` (or `t2.micro`) instance running the Docker image** that CI publishes to GitHub
Container Registry (GHCR). Both instance types are Free Tier eligible; GHCR is free for public images.

Why this setup: it is the simplest Free-Tier option that runs the same container we run locally. App Runner and
Fargate are not Free Tier, and Elastic Beanstalk adds a load balancer that is billed beyond the free hours.

**No credentials live in the repo.** CI pushes the image with the built-in `GITHUB_TOKEN`; the instance pulls a
public image, so it needs no secrets.

## 1. Publish the image

1. Push the repository to GitHub (`main`). The **CI** workflow builds and pushes
   `ghcr.io/<github-user>/<repo>:latest`.
2. On GitHub: your profile → **Packages** → the package → **Package settings** → **Change visibility → Public**.
   (New GHCR packages are private by default; EC2 can't pull them anonymously otherwise.)

## 2. Launch the instance

In the AWS console (region of your choice) → **EC2 → Launch instance**:

| Setting | Value |
|---|---|
| Name | `vehicle-explorer` |
| AMI | Amazon Linux 2023 (Free tier eligible) |
| Instance type | `t3.micro` (or `t2.micro`, whichever is marked *Free tier eligible*) |
| Key pair | Optional — you can use **EC2 Instance Connect** instead |
| Network → Security group | Allow **HTTP (80)** from `0.0.0.0/0`. Allow SSH (22) only from *My IP* if you want SSH. |
| Storage | 8 GB gp3 (default) |
| Advanced details → **User data** | Paste [`deploy/ec2-user-data.sh`](../deploy/ec2-user-data.sh) after replacing `<github-user>/<repo>` |

Launch, wait ~3 minutes for the user-data script to finish, then open `http://<public-IPv4-DNS>/`.

Check it: `curl http://<public-IPv4-DNS>/health` → `Healthy`.

## 3. Redeploying after a change

Push to `main` (CI publishes a new `:latest`), then on the instance (EC2 Instance Connect or SSH):

```bash
sudo /usr/local/bin/redeploy
```

## Troubleshooting

- Logs of the bootstrap: `sudo cat /var/log/cloud-init-output.log`
- App logs: `sudo docker logs vehicle-explorer`
- `pull access denied` → the GHCR package is still private (step 1.2).

## Cost notes

- Stop or terminate the instance when no longer needed; stay within the 750 free instance-hours/month.
- An Elastic IP is free only while attached to a running instance — skip it unless you need a fixed IP
  (the public DNS name changes after a stop/start).
- Optional HTTPS: put a CloudFront distribution in front of the instance (Free Tier includes 1 TB/month).

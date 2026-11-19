#!/bin/bash
# EC2 user data for Amazon Linux 2023 (t3.micro / t2.micro, Free Tier eligible).
# Installs Docker and runs the public image that CI pushes to GitHub Container Registry.
# Contains no credentials: the image is public, so no registry login is needed.
#
# Before using: replace <github-user>/<repo> below with your repository (lowercase).
set -euxo pipefail

IMAGE="ghcr.io/<github-user>/<repo>:latest"

dnf update -y
dnf install -y docker
systemctl enable --now docker

# 1 GB RAM instances: a little swap keeps the box stable.
if [ ! -f /swapfile ]; then
  dd if=/dev/zero of=/swapfile bs=1M count=1024
  chmod 600 /swapfile
  mkswap /swapfile
  swapon /swapfile
  echo '/swapfile swap swap defaults 0 0' >> /etc/fstab
fi

# Helper used for the first start and for later redeploys: `sudo /usr/local/bin/redeploy`
cat > /usr/local/bin/redeploy <<EOF
#!/bin/bash
set -euo pipefail
docker pull ${IMAGE}
docker rm -f vehicle-explorer 2>/dev/null || true
docker run -d --name vehicle-explorer --restart unless-stopped -p 80:8080 ${IMAGE}
docker image prune -f
EOF
chmod +x /usr/local/bin/redeploy

/usr/local/bin/redeploy

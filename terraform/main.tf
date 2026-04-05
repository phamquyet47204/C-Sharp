terraform {
  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 5.0"
    }
  }
}

provider "aws" {
  region = "ap-southeast-1" # Singapore
}

# ── Data: Ubuntu 22.04 LTS AMI mới nhất ──────────────────────
data "aws_ami" "ubuntu" {
  most_recent = true
  owners      = ["099720109477"] # Canonical

  filter {
    name   = "name"
    values = ["ubuntu/images/hvm-ssd/ubuntu-jammy-22.04-amd64-server-*"]
  }

  filter {
    name   = "virtualization-type"
    values = ["hvm"]
  }
}

# ── Security Group ────────────────────────────────────────────
resource "aws_security_group" "vinhkhanh_sg" {
  name        = "vinhkhanh-sg"
  description = "VinhKhanh app security group"

  # SSH
  ingress {
    from_port   = 22
    to_port     = 22
    protocol    = "tcp"
    cidr_blocks = ["0.0.0.0/0"]
  }

  # HTTP
  ingress {
    from_port   = 80
    to_port     = 80
    protocol    = "tcp"
    cidr_blocks = ["0.0.0.0/0"]
  }

  # HTTPS
  ingress {
    from_port   = 443
    to_port     = 443
    protocol    = "tcp"
    cidr_blocks = ["0.0.0.0/0"]
  }

  # Outbound: tất cả
  egress {
    from_port   = 0
    to_port     = 0
    protocol    = "-1"
    cidr_blocks = ["0.0.0.0/0"]
  }

  tags = {
    Name = "vinhkhanh-sg"
  }
}

# ── Key Pair — import từ cs.pem có sẵn ──────────────────────
resource "aws_key_pair" "cs" {
  key_name   = "cs"
  public_key = file("C:/Users/phamq/Documents/key/cs.pub")
}

# ── EC2 Instance ──────────────────────────────────────────────
resource "aws_instance" "vinhkhanh" {
  ami                    = data.aws_ami.ubuntu.id
  instance_type          = "t3.medium"
  key_name               = aws_key_pair.cs.key_name
  vpc_security_group_ids = [aws_security_group.vinhkhanh_sg.id]

  root_block_device {
    volume_size = 30    # GB
    volume_type = "gp3"
  }

  user_data = file("${path.module}/user_data.sh")

  tags = {
    Name = "vinhkhanh-server"
  }
}

# ── Elastic IP (IP tĩnh) ──────────────────────────────────────
resource "aws_eip" "vinhkhanh_eip" {
  instance = aws_instance.vinhkhanh.id
  domain   = "vpc"

  tags = {
    Name = "vinhkhanh-eip"
  }
}

# ── Outputs ───────────────────────────────────────────────────
output "public_ip" {
  value       = aws_eip.vinhkhanh_eip.public_ip
  description = "Trỏ DNS enormitpham.me về IP này"
}

output "ssh_command" {
  value       = "ssh -i cs.pem ubuntu@${aws_eip.vinhkhanh_eip.public_ip}"
  description = "Lệnh SSH vào server"
}

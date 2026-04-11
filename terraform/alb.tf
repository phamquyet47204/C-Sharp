# ============================================================
# ALB + ACM + Route 53 cho enormitpham.me
# Yêu cầu: domain enormitpham.me đã được hosted zone trong Route 53
# ============================================================

# ── Lấy Hosted Zone của enormitpham.me ───────────────────────
data "aws_route53_zone" "main" {
  name         = "enormitpham.me."
  private_zone = false
}

# ── ACM Certificate (SSL) ─────────────────────────────────────
resource "aws_acm_certificate" "cert" {
  domain_name               = "enormitpham.me"
  subject_alternative_names = ["www.enormitpham.me"]
  validation_method         = "DNS"

  lifecycle {
    create_before_destroy = true
  }

  tags = {
    Name = "vinhkhanh-cert"
  }
}

# ── DNS records để validate ACM ──────────────────────────────
resource "aws_route53_record" "cert_validation" {
  for_each = {
    for dvo in aws_acm_certificate.cert.domain_validation_options : dvo.domain_name => {
      name   = dvo.resource_record_name
      record = dvo.resource_record_value
      type   = dvo.resource_record_type
    }
  }

  allow_overwrite = true
  name            = each.value.name
  records         = [each.value.record]
  ttl             = 60
  type            = each.value.type
  zone_id         = data.aws_route53_zone.main.zone_id
}

resource "aws_acm_certificate_validation" "cert" {
  certificate_arn         = aws_acm_certificate.cert.arn
  validation_record_fqdns = [for record in aws_route53_record.cert_validation : record.fqdn]
}

# ── VPC mặc định và Subnets ───────────────────────────────────
data "aws_vpc" "default" {
  default = true
}

data "aws_subnets" "default" {
  filter {
    name   = "vpc-id"
    values = [data.aws_vpc.default.id]
  }
}

# ── Security Group cho ALB ────────────────────────────────────
resource "aws_security_group" "alb_sg" {
  name        = "vinhkhanh-alb-sg"
  description = "ALB security group"
  vpc_id      = data.aws_vpc.default.id

  ingress {
    from_port   = 80
    to_port     = 80
    protocol    = "tcp"
    cidr_blocks = ["0.0.0.0/0"]
  }

  ingress {
    from_port   = 443
    to_port     = 443
    protocol    = "tcp"
    cidr_blocks = ["0.0.0.0/0"]
  }

  egress {
    from_port   = 0
    to_port     = 0
    protocol    = "-1"
    cidr_blocks = ["0.0.0.0/0"]
  }

  tags = { Name = "vinhkhanh-alb-sg" }
}

# ── ALB ───────────────────────────────────────────────────────
resource "aws_lb" "main" {
  name               = "vinhkhanh-alb"
  internal           = false
  load_balancer_type = "application"
  security_groups    = [aws_security_group.alb_sg.id]
  subnets            = data.aws_subnets.default.ids

  tags = { Name = "vinhkhanh-alb" }
}

# ── Target Group → EC2 port 80 ────────────────────────────────
resource "aws_lb_target_group" "app" {
  name     = "vinhkhanh-tg"
  port     = 80
  protocol = "HTTP"
  vpc_id   = data.aws_vpc.default.id

  health_check {
    path                = "/api/health"
    healthy_threshold   = 2
    unhealthy_threshold = 3
    interval            = 30
    timeout             = 10
    matcher             = "200-404"  # 404 vẫn OK vì app đang chạy
  }

  tags = { Name = "vinhkhanh-tg" }
}

# ── Gắn EC2 vào Target Group ──────────────────────────────────
resource "aws_lb_target_group_attachment" "app" {
  target_group_arn = aws_lb_target_group.app.arn
  target_id        = aws_instance.vinhkhanh.id
  port             = 80
}

# ── Listener HTTP → redirect HTTPS ───────────────────────────
resource "aws_lb_listener" "http" {
  load_balancer_arn = aws_lb.main.arn
  port              = 80
  protocol          = "HTTP"

  default_action {
    type = "redirect"
    redirect {
      port        = "443"
      protocol    = "HTTPS"
      status_code = "HTTP_301"
    }
  }
}

# ── Listener HTTPS → forward đến EC2 ─────────────────────────
resource "aws_lb_listener" "https" {
  load_balancer_arn = aws_lb.main.arn
  port              = 443
  protocol          = "HTTPS"
  ssl_policy        = "ELBSecurityPolicy-TLS13-1-2-2021-06"
  certificate_arn   = aws_acm_certificate_validation.cert.certificate_arn

  default_action {
    type             = "forward"
    target_group_arn = aws_lb_target_group.app.arn
  }
}

# ── Route 53: enormitpham.me → ALB (Alias) ───────────────────
resource "aws_route53_record" "apex" {
  zone_id         = data.aws_route53_zone.main.zone_id
  name            = "enormitpham.me"
  type            = "A"
  allow_overwrite = true

  alias {
    name                   = aws_lb.main.dns_name
    zone_id                = aws_lb.main.zone_id
    evaluate_target_health = true
  }
}

resource "aws_route53_record" "www" {
  zone_id = data.aws_route53_zone.main.zone_id
  name    = "www.enormitpham.me"
  type    = "A"

  alias {
    name                   = aws_lb.main.dns_name
    zone_id                = aws_lb.main.zone_id
    evaluate_target_health = true
  }
}

# ── Outputs ───────────────────────────────────────────────────
output "alb_dns" {
  value       = aws_lb.main.dns_name
  description = "ALB DNS name"
}

output "site_url" {
  value       = "https://enormitpham.me"
  description = "URL sau khi DNS propagate"
}

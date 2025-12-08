@REM https://docs.digitalocean.com/products/container-registry/how-to/use-registry-docker-kubernetes/
@REM https://docs.digitalocean.com/reference/doctl/how-to/install/
@REM https://docs.digitalocean.com/reference/api/create-personal-access-token/

@REM doctl auth init distance
@REM doctl registry login

docker build distance_scraper:2.0
docker tag distance_scraper:2.0 registry.digitalocean.com/noodle/distance_scraper:2.0
docker push registry.digitalocean.com/noodle/distance_scraper:2.0

@REM In droplet:
@REM docker pull registry.digitalocean.com/noodle/distance_scraper:2.0
@REM docker run -d --name scraper_2_0 registry.digitalocean.com/noodle/distance_scraper:2.0

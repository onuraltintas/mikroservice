import {readFileSync,writeFileSync} from 'node:fs';
const dir='tools/releases/coaching-followup-20261017/';
let s=readFileSync('tools/releases/coaching-restore-profile-20261017/deploy.sh','utf8').replaceAll('coaching-restore-profile-20261017','coaching-followup-20261017').replaceAll('coaching_restore_profile_drill_20261017','coaching_followup_drill_20261017');
s=s.replace('services=(coaching-service admin-panel staff-portal)','services=(notification-service coaching-service staff-portal)').replaceAll('eduivme-production-admin-panel-1','eduivme-production-notification-service-1');
s=s.replaceAll('sha256:b1dcddda49514ca37f232e9e9faa73d2338491493730067ddb709b02246d1b0d','sha256:33dd74ec73b4d96736d0972032a07f94503f6cb3b3dd93da92e686491f444522').replaceAll('sha256:be9aae3825f10de6de3dcb562fb8f9d2bda889baeca46219df1623e043a8c6c9','sha256:bb0195b628c121eaa71629e7bb5acacc26813d873c7f6dcdad8b05a0b69ce53d').replaceAll('sha256:c53761cf825c90b388fdd9f02a4a111107d5aabf175a060c7ba449d47d495bc7','sha256:ac0b20f5efc8481312e848c908fdacdacd6efebced244855dc5986a58e96ad48');
s=s.replaceAll('/var/lib/eduivme/releases/coaching-calendar-move-20261017-r2/production.compose.json','/var/lib/eduivme/releases/coaching-restore-profile-20261017/production.compose.json');
s=s.replaceAll('.services["admin-panel"].image=("eduivme/admin-panel:"+$tag)','.services["notification-service"].image=("eduivme/notification-service:"+$tag)');
s=s.replace('for database in identity_db coaching_db;','for database in identity_db coaching_db notification_db;');
s=s.replace('    accounts > accounts.live.after.txt; fingerprint coaching_db > coaching.live.after.txt',`    accounts > accounts.live.after.txt; fingerprint coaching_db > coaching.live.after.txt`);
const queue=`queueReady() {
  for attempt in $(seq 1 15); do
    if docker exec rabbitmq rabbitmqctl -q list_queues name consumers --formatter json 2>/dev/null | jq -e '.[]|select(.name=="coaching-study-plan-updated" and .consumers>=1)' > queue-ready.json; then return 0; fi
    sleep 2
  done
  echo 'Plan update consumer is not ready; producer was not promoted.' >&2; return 1
}
`;
s=s.replace('fingerprint() {',queue+'fingerprint() {');
s=s.replace('      waitReady "eduivme-production-$service-1" "$service"','      waitReady "eduivme-production-$service-1" "$service"\n      [[ "$service" != notification-service ]] || queueReady');
writeFileSync(dir+'deploy.sh',s.replaceAll('\r\n','\n'));
for(const f of ['stage.sh','monitor.sh','start-monitor.sh']){
 let t=readFileSync('tools/releases/coaching-restore-profile-20261017/'+f,'utf8').replaceAll('coaching-restore-profile-20261017','coaching-followup-20261017').replaceAll('coaching-service admin-panel staff-portal','notification-service coaching-service staff-portal');
 writeFileSync(dir+f,t.replaceAll('\r\n','\n'));
}

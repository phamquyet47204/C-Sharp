import axios from 'axios';
async function run() {
  const res = await axios.get('https://generativelanguage.googleapis.com/v1beta/models?key=AIzaSyCdMVof971Ylf9Oz0HOX5xQFX6YtAMQeYY');
  const items = res.data.models.map(m => m.name).filter(n => n.includes('gemini'));
  console.log(items);
}
run();

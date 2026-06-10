/**
 * Cloudflare Worker - BattlegroundDB 下载服务
 * 
 * 端点：
 *   GET /          - 版本信息
 *   GET /version   - 仅返回版本号
 *   GET /download  - 下载 DLL 文件
 */

export default {
  async fetch(request, env) {
    const url = new URL(request.url);
    const path = url.pathname;

    // CORS 头
    const corsHeaders = {
      'Access-Control-Allow-Origin': '*',
      'Access-Control-Allow-Methods': 'GET, OPTIONS',
      'Access-Control-Allow-Headers': 'If-None-Match, If-Modified-Since',
    };

    // 处理 OPTIONS 请求
    if (request.method === 'OPTIONS') {
      return new Response(null, { headers: corsHeaders });
    }

    // 路由处理
    try {
      if (path === '/' || path === '/version') {
        return handleVersion(env, corsHeaders);
      }

      if (path === '/download') {
        return handleDownload(request, env, corsHeaders);
      }

      return new Response('Not Found', { status: 404, headers: corsHeaders });
    } catch (error) {
      return new Response(`Error: ${error.message}`, { 
        status: 500, 
        headers: corsHeaders 
      });
    }
  }
};

/**
 * 处理版本信息请求
 */
async function handleVersion(env, headers) {
  const version = env.BGDB_VERSION || '2.0.0';
  const filename = env.BGDB_FILENAME || 'BattlegroundDB.dll';
  
  // 从 KV 获取更新时间
  const updatedAt = await env.BGDB_KV?.get('updatedAt') || null;
  
  const response = {
    version: version,
    filename: filename,
    downloadUrl: `/download`,
    updatedAt: updatedAt,
  };

  return new Response(JSON.stringify(response, null, 2), {
    headers: {
      ...headers,
      'Content-Type': 'application/json',
      'Cache-Control': 'public, max-age=3600',  // 缓存 1 小时
    }
  });
}

/**
 * 处理 DLL 下载请求
 */
async function handleDownload(request, env, headers) {
  const version = env.BGDB_VERSION || '2.0.0';
  const filename = env.BGDB_FILENAME || 'BattlegroundDB.dll';

  // 从 KV 获取 DLL 文件
  const dllKey = `dll/${version}`;
  const dll = await env.BGDB_KV?.get(dllKey, { type: 'arrayBuffer' });

  if (!dll) {
    return new Response('DLL not found. Please upload the DLL to KV first.', {
      status: 404,
      headers: {
        ...headers,
        'Content-Type': 'text/plain',
      }
    });
  }

  // 生成 ETag（基于版本号）
  const etag = `"${version}"`;

  // 检查 If-None-Match（304 缓存）
  const ifNoneMatch = request.headers.get('If-None-Match');
  if (ifNoneMatch === etag) {
    return new Response(null, {
      status: 304,
      headers: {
        ...headers,
        'ETag': etag,
      }
    });
  }

  // 返回 DLL 文件
  return new Response(dll, {
    headers: {
      ...headers,
      'Content-Type': 'application/octet-stream',
      'Content-Disposition': `attachment; filename="${filename}"`,
      'Content-Length': dll.byteLength.toString(),
      'ETag': etag,
      'Cache-Control': 'public, max-age=86400',  // 缓存 24 小时
    }
  });
}

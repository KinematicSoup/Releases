mergeInto(LibraryManager.library, {      
    KSCreateWS: function(onConnect, onDisconnect, onMessage) {
        this.nextId = this.nextId || 1;
        this.instances = this.instances || {};
        
        var id = this.nextId++;
        var webSocket = {   
            id: -1,
            ws: null,
            
            // Read buffers
            buffers: [],
            bufferTotal: 0,
            bufferOffset: 0,
            
            // Event handlers
            OnDisconnect: null,
            OnConnect: null,
            OnMessage: null,
            
            Connect: function(url) {
                this.ws = new WebSocket(UTF8ToString(url), "KSReactor");
                this.ws.binaryType = 'arraybuffer';
                this.ws.onerror = this.HandleError.bind(this);
                this.ws.onopen = this.HandleOpen.bind(this);        
                this.ws.onclose = this.HandleClose.bind(this);        
                this.ws.onmessage = this.HandleMessage.bind(this);
            },
                
            Disconnect: function() {
                this.ws.close();
            },
            
            HandleError: function(error) {
                console.error('KSWebSocket (' + this.id + ') error: ' + error);
            },
            
            HandleOpen: function(event) {
                Module['dynCall_vi'](this.OnConnect, this.id);
            },
                
            HandleClose: function(event) {
                Module['dynCall_vii'](this.OnDisconnect, this.id, event.code);
            },
                
            HandleMessage: function(event) {
                this.buffers.push(new Uint8Array(event.data));
                this.bufferTotal += event.data.byteLength;
                Module['dynCall_vii'](this.OnMessage, this.id, this.bufferTotal);
            },
            
            Read: function(data, offset, count) {
                if (count > this.bufferTotal) {
                    return this.bufferTotal;
                }
                
                var remaining = count;
                while (remaining > 0) {
                    var buffer = this.buffers[0];
                    
                    var amount = Math.min(remaining, buffer.byteLength - this.bufferOffset);
                    HEAPU8.set(buffer.subarray(this.bufferOffset, this.bufferOffset+amount), data+offset);
                    this.bufferOffset += amount;
                    this.bufferTotal -= amount;
                    remaining -= amount;
                    offset += amount;
                    
                    if (this.bufferOffset == buffer.byteLength) {
                        this.buffers.shift();
                        this.bufferOffset = 0;
                    }
                }
                
                return this.bufferTotal;
            },
            
            Write: function(data, offset, count) {
                this.ws.send(HEAPU8.buffer.slice(data + offset, data + offset + count));
            }
        };
        
        webSocket.id = id;
        webSocket.OnDisconnect = onDisconnect;
        webSocket.OnConnect = onConnect;
        webSocket.OnMessage = onMessage;
        this.instances[id] = webSocket;
        return id;
    },
    
    KSDisposeWS: function(id){
        if (typeof this.instances[id] !== 'undefined') {
            delete this.instances[id];
        }       
    },
    
    KSConnectWS: function(id, url) {
        if (typeof this.instances[id] !== 'undefined') {
            this.instances[id].Connect(url);
        }
    },
    
    KSDisconnectWS: function(id) {
        if (typeof this.instances[id] !== 'undefined') {
            this.instances[id].Disconnect();
        }
    },
    
    KSReadWS: function(id, data, offset, count) {
        if (typeof this.instances[id] !== 'undefined') {
            return this.instances[id].Read(data, offset, count);
        }
        return 0;
    },
    
    KSWriteWS: function(id, data, offset, count) {
        if (typeof this.instances[id] !== 'undefined') {
            this.instances[id].Write(data, offset, count);
        }
    }
});
